#if IOS
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using ObjCRuntime;
using Foundation;
using AppAmbit;
using System.Diagnostics;
using UserNotifications;
using UIKit;

namespace AppAmbit.PushNotifications;

[SupportedOSPlatform("ios12.0")]
internal static class PushNotificationsIos
{
    private const string NativeClassName = "PushKernel";
    private const string PushNotificationsClassName = "PushNotifications";
    private static IntPtr _classHandle;
    private static IntPtr _pushNotificationsClassHandle;

    // Selectors for PushKernel
    private static IntPtr _selSetDebugMode;
    private static IntPtr _selSetupSwizzling;
    private static IntPtr _selRequestNotificationPermission;
    private static IntPtr _selSetNotificationsEnabled;
    private static IntPtr _selIsNotificationsEnabled;
    private static IntPtr _selSetTokenListener;
    private static IntPtr _selHasNotificationPermission;

    // Selectors for PushNotifications class
    private static IntPtr _selSetNotificationListener;

    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;

    // Notification listener block
    private static IntPtr _notificationListenerBlockPtr = IntPtr.Zero;
    private static object? _listenerAction;

    // Hold reference to listener to prevent GC
    private static object? _tokenListener;

    private static void InvokeOnMainThreadSafe(Action action)
    {
        NSRunLoop.Main.BeginInvokeOnMainThread(action);
    }

    private static void EnsureNativeAvailable()
    {
        if (_classHandle == IntPtr.Zero)
        {
            Debug.WriteLine("[AppAmbit] Initializing native Push SDK...");

            LoadNativeFrameworks();

            Debug.WriteLine("[AppAmbit] Getting PushKernel class handle...");
            _classHandle = Class.GetHandle(NativeClassName);

            if (_classHandle == IntPtr.Zero)
            {
                var errorMsg = $"AppAmbitPushNotifications native class '{NativeClassName}' is not available. Native frameworks may be missing or failed to load.";
                Debug.WriteLine($"[AppAmbit] ERROR: {errorMsg}");
                throw new PlatformNotSupportedException(errorMsg);
            }

            Debug.WriteLine($"[AppAmbit] Successfully got PushKernel class handle: {_classHandle}");

            Debug.WriteLine("[AppAmbit] Getting PushNotifications class handle...");
            _pushNotificationsClassHandle = Class.GetHandle(PushNotificationsClassName);

            if (_pushNotificationsClassHandle == IntPtr.Zero)
            {
                var errorMsg = $"AppAmbitPushNotifications native class '{PushNotificationsClassName}' is not available.";
                Debug.WriteLine($"[AppAmbit] ERROR: {errorMsg}");
                throw new PlatformNotSupportedException(errorMsg);
            }

            Debug.WriteLine($"[AppAmbit] Successfully got PushNotifications class handle: {_pushNotificationsClassHandle}");

            InitializeSelectors();
            Debug.WriteLine("[AppAmbit] Selectors initialized successfully");
        }
    }

    private static void LoadNativeFrameworks()
    {
        try
        {
            var bundlePath = NSBundle.MainBundle.BundlePath;
            Debug.WriteLine($"[AppAmbit] Bundle path: {bundlePath}");

            var sdkPath  = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbit.framework", "AppAmbit");
            var pushPath = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbitPushNotifications.framework", "AppAmbitPushNotifications");

            Debug.WriteLine($"[AppAmbit] Loading SDK framework from: {sdkPath} (exists={System.IO.File.Exists(sdkPath)})");
            var sdkHandle = dlopen(sdkPath, 2);
            if (sdkHandle == IntPtr.Zero)
                Debug.WriteLine($"[AppAmbit] ERROR: Failed to load SDK framework. dlerror={Marshal.PtrToStringAnsi(dlerror())}");
            else
                Debug.WriteLine("[AppAmbit] SDK framework loaded");

            Debug.WriteLine($"[AppAmbit] Loading Push framework from: {pushPath} (exists={System.IO.File.Exists(pushPath)})");
            var pushHandle = dlopen(pushPath, 2);
            if (pushHandle == IntPtr.Zero)
                Debug.WriteLine($"[AppAmbit] ERROR: Failed to load Push framework. dlerror={Marshal.PtrToStringAnsi(dlerror())}");
            else
                Debug.WriteLine("[AppAmbit] Push framework loaded");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit] FATAL: Error loading native frameworks: {ex}");
            throw;
        }
    }

    private static void InitializeSelectors()
    {
        _selSetDebugMode                  = Selector.GetHandle("setDebugMode:");
        _selSetupSwizzling                = Selector.GetHandle("setupSwizzling");
        _selRequestNotificationPermission = Selector.GetHandle("requestNotificationPermissionWithListener:");
        _selSetNotificationsEnabled       = Selector.GetHandle("setNotificationsEnabled:");
        _selIsNotificationsEnabled        = Selector.GetHandle("isNotificationsEnabled");
        _selSetTokenListener              = Selector.GetHandle("setTokenListener:");
        _selHasNotificationPermission     = Selector.GetHandle("hasNotificationPermission");
        _selSetNotificationListener       = Selector.GetHandle("setNotificationListener:");
    }

    public static void Start()
    {
        EnsureNativeAvailable();

        if (_initialized) return;
        _initialized = true;

        objc_msgSend_bool(_classHandle, _selSetDebugMode, true);

        var listener = new TokenListenerImpl();
        objc_msgSend_IntPtr(_classHandle, _selSetTokenListener, listener.Handle);
        _tokenListener = listener;

        InvokeOnMainThreadSafe(() =>
        {
            var app = UIApplication.SharedApplication;
            var del = app?.Delegate;

            if (app != null && del != null)
                app.Delegate = null;

            objc_msgSend(_classHandle, _selSetupSwizzling);

            if (app != null)
            {
                if (del != null)
                    app.Delegate = del;

                app.RegisterForRemoteNotifications();
            }
        });

        UNUserNotificationCenter.Current.GetNotificationSettings((settings) =>
        {
            bool isGranted = settings.AuthorizationStatus == UNAuthorizationStatus.Authorized ||
                             settings.AuthorizationStatus == UNAuthorizationStatus.Provisional;
            NSUserDefaults.StandardUserDefaults.SetBool(isGranted, "AppAmbit.Push.HasPermission");
        });
    }

    public static void SetNotificationsEnabled(bool enabled)
    {
        EnsureNativeAvailable();

        if (!_initialized)
        {
            Debug.WriteLine("[AppAmbit] SetNotificationsEnabled called before Start(). Auto-initializing...");
            Start();
        }

        objc_msgSend_bool(_classHandle, _selSetNotificationsEnabled, enabled);

        if (enabled)
        {
            InvokeOnMainThreadSafe(() =>
            {
                UIApplication.SharedApplication.RegisterForRemoteNotifications();
            });
        }

        var token = _lastPushToken;
        _ = Task.Run(async () =>
        {
            try
            {
                await AppAmbitSdk.UpdateConsumerAsync(token, enabled);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LogTag}: Sync error: {ex}");
            }
        });
    }

    public static bool IsNotificationsEnabled()
    {
        EnsureNativeAvailable();
        return objc_msgSend_bool_ret(_classHandle, _selIsNotificationsEnabled);
    }

    public static bool HasSystemPermission()
    {
        EnsureNativeAvailable();
        bool hasPerm = NSUserDefaults.StandardUserDefaults.BoolForKey("AppAmbit.Push.HasPermission");
        _ = objc_msgSend_bool_ret(_classHandle, _selHasNotificationPermission);
        Debug.WriteLine($"{LogTag}: HasSystemPermission (C# Pref) = {hasPerm}");
        return hasPerm;
    }

    public static string? GetCurrentToken()
    {
        EnsureNativeAvailable();
        var sel = Selector.GetHandle("getCurrentToken");
        var tokenPtr = objc_msgSend_IntPtr_ret(_classHandle, sel);
        return tokenPtr != IntPtr.Zero ? NSString.FromHandle(tokenPtr) : null;
    }

    private static System.Collections.Generic.List<object> _pendingCallbacks = new();

    public static void RequestNotificationPermission(Action<bool>? callback)
    {
        EnsureNativeAvailable();

        Debug.WriteLine($"{LogTag}: RequestNotificationPermission called. Callback is null? {callback == null}");

        if (callback != null)
        {
            lock (_pendingCallbacks)
                _pendingCallbacks.Add(callback);
        }

        Debug.WriteLine($"{LogTag}: Requesting permission via UNUserNotificationCenter.");

        var center  = UNUserNotificationCenter.Current;
        var options = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound;

        center.RequestAuthorization(options, (granted, error) =>
        {
            if (error != null)
                Debug.WriteLine($"{LogTag}: Error requesting permission: {error.LocalizedDescription}");

            Debug.WriteLine($"{LogTag}: (C# Direct) Permission Result: {granted}");

            if (granted)
            {
                NSUserDefaults.StandardUserDefaults.SetBool(true, "AppAmbit.Push.HasPermission");
                InvokeOnMainThreadSafe(() =>
                {
                    UIApplication.SharedApplication.RegisterForRemoteNotifications();
                });
                SetNotificationsEnabled(true);
            }

            if (callback != null)
            {
                try
                {
                    Debug.WriteLine($"{LogTag}: Invoking user callback...");
                    callback(granted);
                    Debug.WriteLine($"{LogTag}: User callback invoked.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LogTag}: Error invoking user callback: {ex}");
                }
                finally
                {
                    lock (_pendingCallbacks)
                        _pendingCallbacks.Remove(callback);
                }
            }
        });
    }

    // ── SetNotificationListener ────────────────────────────────────────────

    private delegate void NotificationListenerDelegate(IntPtr block, IntPtr userInfoPtr, nint state);

    [ObjCRuntime.MonoPInvokeCallback(typeof(NotificationListenerDelegate))]
    private static void NotificationListenerTrampoline(IntPtr block, IntPtr userInfoPtr, nint state)
    {
        try
        {
            var action = BlockLiteral.GetTarget<Action<NSDictionary, PushNotificationState>>(block);
            if (action == null) return;
            var userInfo = userInfoPtr != IntPtr.Zero
                ? Runtime.GetNSObject<NSDictionary>(userInfoPtr) ?? new NSDictionary()
                : new NSDictionary();
            action(userInfo, (PushNotificationState)(int)state);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit] Error in notification listener trampoline: {ex}");
        }
    }

    public static void SetNotificationListener(Action<NSDictionary, PushNotificationState> listener)
    {
        EnsureNativeAvailable();

        if (_notificationListenerBlockPtr != IntPtr.Zero)
        {
            unsafe
            {
                BlockLiteral* old = (BlockLiteral*)_notificationListenerBlockPtr;
                old->CleanupBlock();
                Marshal.FreeHGlobal(_notificationListenerBlockPtr);
            }
            _notificationListenerBlockPtr = IntPtr.Zero;
        }

        _listenerAction = listener;

        unsafe
        {
            _notificationListenerBlockPtr = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
            BlockLiteral* blockPtr = (BlockLiteral*)_notificationListenerBlockPtr;
            blockPtr->SetupBlockUnsafe(NotificationListenerTrampoline, listener);
            objc_msgSend_IntPtr(_pushNotificationsClassHandle, _selSetNotificationListener, _notificationListenerBlockPtr);
        }
    }

    // ── Internal Token Listener ────────────────────────────────────────────

    [Register("TokenListenerImpl")]
    [Preserve(AllMembers = true)]
    internal class TokenListenerImpl : NSObject
    {
        [Export("onNewToken:")]
        public void OnNewToken(NSString token)
        {
            var tokenStr = token.ToString();
            _lastPushToken = tokenStr;

            Console.WriteLine($"{LogTag}: (C#) Received Token: {tokenStr}");

            _ = Task.Run(async () =>
            {
                try
                {
                    var isEnabled = PushNotificationsIos.IsNotificationsEnabled();
                    Console.WriteLine($"{LogTag}: (C#) Syncing token. Enabled? {isEnabled}");
                    await AppAmbitSdk.UpdateConsumerAsync(tokenStr, isEnabled);
                    Console.WriteLine($"{LogTag}: (C#) Token synced.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{LogTag}: Error syncing token: {ex.Message}");
                }
            });
        }
    }

    // ── P/Invokes ──────────────────────────────────────────────────────────

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool value);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool objc_msgSend_bool_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlerror();
}

#endif
