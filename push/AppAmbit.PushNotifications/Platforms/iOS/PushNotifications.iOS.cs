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
internal static partial class PushNotificationsIos
{
    private const string NativeClassName = "PushKernel";
    private const string PushNotificationsClassName = "PushNotifications";
    private static IntPtr _classHandle;
    private static IntPtr _pushNotificationsClassHandle;

    private static IntPtr _selSetDebugMode;
    private static IntPtr _selSetupSwizzling;
    private static IntPtr _selRequestNotificationPermission;
    private static IntPtr _selSetNotificationsEnabled;
    private static IntPtr _selIsNotificationsEnabled;
    private static IntPtr _selSetTokenListener;
    private static IntPtr _selHasNotificationPermission;
    private static IntPtr _selSetNotificationListener;

    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;

    private static object? _tokenListener;
    private static readonly System.Collections.Generic.List<object> _pendingCallbacks = new();

    private static void InvokeOnMainThreadSafe(Action action) =>
        NSRunLoop.Main.BeginInvokeOnMainThread(action);

    private static void EnsureNativeAvailable()
    {
        if (_classHandle != IntPtr.Zero) return;

        Debug.WriteLine("[AppAmbit] Initializing native Push SDK...");
        LoadNativeFrameworks();

        _classHandle = Class.GetHandle(NativeClassName);
        if (_classHandle == IntPtr.Zero)
        {
            var msg = $"AppAmbitPushNotifications native class '{NativeClassName}' is not available.";
            Debug.WriteLine($"[AppAmbit] ERROR: {msg}");
            throw new PlatformNotSupportedException(msg);
        }

        _pushNotificationsClassHandle = Class.GetHandle(PushNotificationsClassName);
        if (_pushNotificationsClassHandle == IntPtr.Zero)
        {
            var msg = $"AppAmbitPushNotifications native class '{PushNotificationsClassName}' is not available.";
            Debug.WriteLine($"[AppAmbit] ERROR: {msg}");
            throw new PlatformNotSupportedException(msg);
        }

        InitializeSelectors();
        Debug.WriteLine("[AppAmbit] Selectors initialized successfully");
    }

    private static void LoadNativeFrameworks()
    {
        var bundlePath = NSBundle.MainBundle.BundlePath;
        var sdkPath  = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbit.framework", "AppAmbit");
        var pushPath = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbitPushNotifications.framework", "AppAmbitPushNotifications");

        if (dlopen(sdkPath, 2) == IntPtr.Zero)
            Debug.WriteLine($"[AppAmbit] ERROR: Failed to load SDK framework. dlerror={Marshal.PtrToStringAnsi(dlerror())}");

        if (dlopen(pushPath, 2) == IntPtr.Zero)
            Debug.WriteLine($"[AppAmbit] ERROR: Failed to load Push framework. dlerror={Marshal.PtrToStringAnsi(dlerror())}");
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

        AppAmbitSdk.RegisterPushConnectivityHook(() =>
        {
            // PushKernel.handleNewToken deduplicates: it only calls back when the token
            // CHANGES from its cached value. On cold start after an offline enable/disable
            // sequence, OnNewToken is never fired because APNs re-delivers the same token.
            // Fall back to GetCurrentToken() (the native SDK's persisted cached value) so
            // we always have a token to sync even when _lastPushToken is null.
            var token = _lastPushToken ?? GetCurrentToken();

            // If still no token but push is enabled, nudge the native SDK to activate
            // in case it was left in a deferred state (e.g. set-enabled while offline).
            if (token == null && IsNotificationsEnabled())
                InvokeOnMainThreadSafe(() =>
                    objc_msgSend_bool(_classHandle, _selSetNotificationsEnabled, true));

            return AppAmbitSdk.UpdateConsumerAsync(token, IsNotificationsEnabled());
        });

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

                // Restore the native SDK's in-memory enabled state from the persisted
                // preference before requesting the token. The native SDK resets its
                // enabled flag on every cold start, so without this it silently drops
                // the didRegisterForRemoteNotificationsWithDeviceToken callback and
                // OnNewToken is never called — leaving _lastPushToken null and causing
                // the stored (potentially stale) token to be re-used on the next sync.
                if (IsNotificationsEnabled())
                    objc_msgSend_bool(_classHandle, _selSetNotificationsEnabled, true);

                app.RegisterForRemoteNotifications();
            }
        });

        UNUserNotificationCenter.Current.GetNotificationSettings(settings =>
        {
            bool isGranted = settings.AuthorizationStatus == UNAuthorizationStatus.Authorized ||
                             settings.AuthorizationStatus == UNAuthorizationStatus.Provisional;
            NSUserDefaults.StandardUserDefaults.SetBool(isGranted, "AppAmbit.Push.HasPermission");
        });
    }

    private const string PrefKeyIsEnabled    = "AppAmbit.Push.IsEnabled";
    private const string PrefKeyIsEnabledSet = "AppAmbit.Push.IsEnabledSet";

    public static void SetNotificationsEnabled(bool enabled)
    {
        EnsureNativeAvailable();

        if (!_initialized)
            Start();

        objc_msgSend_bool(_classHandle, _selSetNotificationsEnabled, enabled);

        // Persist so IsNotificationsEnabled() survives cold restarts independently
        // of whether the native SDK persists its own in-memory state.
        NSUserDefaults.StandardUserDefaults.SetBool(enabled, PrefKeyIsEnabled);
        NSUserDefaults.StandardUserDefaults.SetBool(true, PrefKeyIsEnabledSet);

        if (enabled)
            InvokeOnMainThreadSafe(() => UIApplication.SharedApplication.RegisterForRemoteNotifications());

        // _lastPushToken is set by OnNewToken. On subsequent launches iOS won't re-deliver
        // an unchanged token, so _lastPushToken stays null for the session. Fall back to
        // GetCurrentToken() (native cached value) so the consumer update is not skipped.
        var token = enabled ? (_lastPushToken ?? GetCurrentToken()) : _lastPushToken;
        _ = Task.Run(async () =>
        {
            try { await AppAmbitSdk.UpdateConsumerAsync(token, enabled); }
            catch (Exception ex) { Debug.WriteLine($"{LogTag}: Sync error: {ex}"); }
        });
    }

    public static bool IsNotificationsEnabled()
    {
        EnsureNativeAvailable();
        // NSUserDefaults is the primary source of truth when the user has explicitly
        // called SetNotificationsEnabled. The native SDK resets its in-memory state on
        // cold restart, so we cannot rely on it alone.
        if (NSUserDefaults.StandardUserDefaults.BoolForKey(PrefKeyIsEnabledSet))
            return NSUserDefaults.StandardUserDefaults.BoolForKey(PrefKeyIsEnabled);
        return objc_msgSend_bool_ret(_classHandle, _selIsNotificationsEnabled);
    }

    public static bool HasNotificationPermission()
    {
        EnsureNativeAvailable();
        bool hasPerm = NSUserDefaults.StandardUserDefaults.BoolForKey("AppAmbit.Push.HasPermission");
        _ = objc_msgSend_bool_ret(_classHandle, _selHasNotificationPermission);
        return hasPerm;
    }

    public static string? GetCurrentToken()
    {
        EnsureNativeAvailable();
        var sel = Selector.GetHandle("getCurrentToken");
        var ptr = objc_msgSend_IntPtr_ret(_classHandle, sel);
        return ptr != IntPtr.Zero ? NSString.FromHandle(ptr) : null;
    }

    public static void RequestNotificationPermission(Action<bool>? callback)
    {
        EnsureNativeAvailable();

        if (callback != null)
            lock (_pendingCallbacks) _pendingCallbacks.Add(callback);

        var center  = UNUserNotificationCenter.Current;
        var options = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound;

        center.RequestAuthorization(options, (granted, error) =>
        {
            if (error != null)
                Debug.WriteLine($"{LogTag}: Error requesting permission: {error.LocalizedDescription}");

            if (granted)
            {
                NSUserDefaults.StandardUserDefaults.SetBool(true, "AppAmbit.Push.HasPermission");
                SetNotificationsEnabled(true);
            }

            if (callback != null)
            {
                try { callback(granted); }
                catch (Exception ex) { Debug.WriteLine($"{LogTag}: Error invoking callback: {ex}"); }
                finally { lock (_pendingCallbacks) _pendingCallbacks.Remove(callback); }
            }
        });
    }
}

#endif
