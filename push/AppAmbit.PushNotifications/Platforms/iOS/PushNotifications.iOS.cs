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
    // Point back to the mechanial PushKernel class
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
    private static IntPtr _selSetNotificationCustomizer;

    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;
    private static PushNotifications.INotificationCustomizer? _customizer;
    private static IntPtr _customizerBlockPtr = IntPtr.Zero;
    
    // Hold reference to listener to prevent GC
    private static object? _tokenListener;
    // PermissionListener manages its own lifetime via GCHandle now

    /// <summary>
    /// Cross-platform main thread dispatch helper.
    /// Uses NSRunLoop directly to ensure compatibility with both MAUI and Native/Avalonia.
    /// </summary>
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
            
            // Get PushNotifications class handle for setNotificationCustomizer
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
            
            var sdkPath = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbit.framework", "AppAmbit");
            var pushPath = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbitPushNotifications.framework", "AppAmbitPushNotifications");

            Debug.WriteLine($"[AppAmbit] Trying to load SDK framework from: {sdkPath}");
            Debug.WriteLine($"[AppAmbit] SDK framework exists: {System.IO.File.Exists(sdkPath)}");

            // 1. Load Sdk - Use RTLD_NOW (2) for iOS
            var sdkHandle = dlopen(sdkPath, 2);
            if (sdkHandle == IntPtr.Zero)
            {
                var error = Marshal.PtrToStringAnsi(dlerror());
                Debug.WriteLine($"[AppAmbit] ERROR: Failed to load Sdk framework. Error: {error}");
            }
            else
            {
                Debug.WriteLine($"[AppAmbit] Successfully loaded SDK framework");
            }

            Debug.WriteLine($"[AppAmbit] Trying to load Push framework from: {pushPath}");
            Debug.WriteLine($"[AppAmbit] Push framework exists: {System.IO.File.Exists(pushPath)}");

            // 2. Load Push
            var pushHandle = dlopen(pushPath, 2);
            if (pushHandle == IntPtr.Zero)
            {
                var error = Marshal.PtrToStringAnsi(dlerror());
                Debug.WriteLine($"[AppAmbit] ERROR: Failed to load Push framework. Error: {error}");
            }
            else
            {
                Debug.WriteLine($"[AppAmbit] Successfully loaded Push framework");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit] FATAL: Error loading native frameworks: {ex}");
            throw;
        }
    }

    private static void InitializeSelectors()
    {
        _selSetDebugMode = Selector.GetHandle("setDebugMode:");
        _selSetupSwizzling = Selector.GetHandle("setupSwizzling");
        _selRequestNotificationPermission = Selector.GetHandle("requestNotificationPermissionWithListener:");
        _selSetNotificationsEnabled = Selector.GetHandle("setNotificationsEnabled:");
        _selIsNotificationsEnabled = Selector.GetHandle("isNotificationsEnabled");
        _selSetTokenListener = Selector.GetHandle("setTokenListener:");
        _selHasNotificationPermission = Selector.GetHandle("hasNotificationPermission");
        _selSetNotificationCustomizer = Selector.GetHandle("setNotificationCustomizer:");
    }

    public static void Start()
    {
        EnsureNativeAvailable();

        if (_initialized) return;
        _initialized = true;

        // 1. Enable Debug Mode (Logs)
        objc_msgSend_bool(_classHandle, _selSetDebugMode, true);

        // 2. Set Token Listener (C# Implementation)
        _tokenListener = new TokenListenerImpl();
        var listener = new TokenListenerImpl();
        objc_msgSend_IntPtr(_classHandle, _selSetTokenListener, listener.Handle);
        _tokenListener = listener; // Keep alive

        InvokeOnMainThreadSafe(() => 
        {
            var app = UIApplication.SharedApplication;
            var del = app?.Delegate;

            // To avoid Native Objective-C/Swift double-swizzling bugs AND to flush Apple's 
            // respondsToSelector: cache, we temporarily detach the delegate before swizzling,
            // then reattach it so the swizzler catches it exactly once.
            if (app != null && del != null)
            {
                app.Delegate = null;
            }

            // 3. Setup Swizzling
            objc_msgSend(_classHandle, _selSetupSwizzling);

            if (app != null)
            {
                if (del != null)
                {
                    app.Delegate = del;
                }

                // 4. Zero-Config APNs Registration
                // We unconditionally request the APNs token on boot. This does not prompt the user
                // for permissions, but ensures the token is available for silent pushes.
                app.RegisterForRemoteNotifications();
            }
        });

        // 5. Heal system permission locally 
        UNUserNotificationCenter.Current.GetNotificationSettings((settings) => 
        {
            bool isGranted = settings.AuthorizationStatus == UNAuthorizationStatus.Authorized || 
                             settings.AuthorizationStatus == UNAuthorizationStatus.Provisional;
            NSUserDefaults.StandardUserDefaults.SetBool(isGranted, "AppAmbit.Push.HasPermission");
            // Do NOT auto-enable here: we explicitly want to respect the user's manual in-app opt-out choice
        });
    }

    public static void SetNotificationsEnabled(bool enabled)
    {
        EnsureNativeAvailable();

        // Guard: auto-initialize if Start() was skipped or called out of order
        if (!_initialized)
        {
            Debug.WriteLine($"[AppAmbit] SetNotificationsEnabled called before Start(). Auto-initializing...");
            Start();
        }

        objc_msgSend_bool(_classHandle, _selSetNotificationsEnabled, enabled);

        // Request APNs token when explicitly enabled
        if (enabled)
        {
            InvokeOnMainThreadSafe(() => 
            {
                UIApplication.SharedApplication.RegisterForRemoteNotifications();
            });
        }

        // Update consumer state with backend (fire and forget with error handling)
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

        // Token is intentionally kept in memory even if disabled locally
        // so that it can be synced back as enabled=true if the user toggles again in the same session.
    }

    public static bool IsNotificationsEnabled()
    {
        EnsureNativeAvailable();
        return objc_msgSend_bool_ret(_classHandle, _selIsNotificationsEnabled);
    }

    public static bool HasSystemPermission()
    {
        EnsureNativeAvailable();
        
        // The native swift method uses an async cache which is often false on reboot.
        // We track it synchronously and reliably here in C#.
        bool hasPerm = NSUserDefaults.StandardUserDefaults.BoolForKey("AppAmbit.Push.HasPermission");
        
        // Call native just to trigger its internal async update (for its own sake), but ignore it
        _ = objc_msgSend_bool_ret(_classHandle, _selHasNotificationPermission);
        
        Debug.WriteLine($"{LogTag}: HasSystemPermission (C# Pref) = {hasPerm}");
        return hasPerm;
    }

    public static string? GetCurrentToken()
    {
        EnsureNativeAvailable();
        // Assuming PushKernel exposes getCurrentToken as a string
        var sel = Selector.GetHandle("getCurrentToken");
        var tokenPtr = objc_msgSend_IntPtr_ret(_classHandle, sel);
        if (tokenPtr != IntPtr.Zero)
            return NSString.FromHandle(tokenPtr);
        return null;
    }

    private static System.Collections.Generic.List<object> _pendingCallbacks = new System.Collections.Generic.List<object>();

    public static void RequestNotificationPermission(Action<bool>? callback)
    {
        EnsureNativeAvailable();
        
        Debug.WriteLine($"{LogTag}: RequestNotificationPermission called. Callback is null? {callback == null}");
        
        // DIRECT IMPLEMENTATION: The native SDK's requestNotificationPermissionWithListener 
        // fails to callback the C# listener in Avalonia (likely ABI/Registrar mismatch).
        // we use the standard iOS API directly, which is robust and triggers the same system behavior.
        
        // Retain the callback to prevent premature GC
        if (callback != null)
        {
            lock (_pendingCallbacks)
            {
                _pendingCallbacks.Add(callback);
            }
        }
        
        Debug.WriteLine($"{LogTag}: Requesting permission via UNUserNotificationCenter directly.");
        
        var center = UNUserNotificationCenter.Current;
        var options = UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound;
        
        center.RequestAuthorization(options, (granted, error) =>
        {
            if (error != null)
            {
                Debug.WriteLine($"{LogTag}: Error requesting permission: {error.LocalizedDescription}");
            }
            
            Debug.WriteLine($"{LogTag}: (C# Direct) Permission Result: {granted}");
            
            // 1. Sync Native Logic (Register APNs)
            if (granted)
            {
                NSUserDefaults.StandardUserDefaults.SetBool(true, "AppAmbit.Push.HasPermission");

                InvokeOnMainThreadSafe(() => 
                {
                    UIApplication.SharedApplication.RegisterForRemoteNotifications();
                });
                
                // 2. Update SDK State
                SetNotificationsEnabled(true);
            }
            
            // 3. User Callback (Call directly, let user handle thread dispatch if needed)
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
                    // Release the callback
                    lock (_pendingCallbacks)
                    {
                        _pendingCallbacks.Remove(callback);
                    }
                }
            }
            else 
            {
                 Debug.WriteLine($"{LogTag}: Callback was null inside closure.");
            }
        });
    }

    private static Action<UNNotification>? _customizerAction;

    public static void SetNotificationCustomizer(PushNotifications.INotificationCustomizer? customizer)
    {
        EnsureNativeAvailable();
        
        // Clean up previous block if it exists
        if (_customizerBlockPtr != IntPtr.Zero)
        {
            unsafe
            {
                BlockLiteral* blockPtr = (BlockLiteral*)_customizerBlockPtr;
                blockPtr->CleanupBlock();
                Marshal.FreeHGlobal(_customizerBlockPtr);
            }
            _customizerBlockPtr = IntPtr.Zero;
        }
        
        if (customizer != null)
        {
            _customizer = customizer; // Keep C# ref
            
            // Create the Action that will be called
            _customizerAction = notification =>
            {
                try
                {
                    Debug.WriteLine($"[AppAmbit] Notification customizer called - Title: {notification?.Request?.Content?.Title}");
                    
                    var content = notification?.Request?.Content;
                    if (content == null) return;
                    
                    var data = new System.Collections.Generic.Dictionary<string, object>();
                    if (content.UserInfo != null)
                    {
                        foreach (var key in content.UserInfo.Keys)
                        {
                            if (key is NSString k)
                            {
                                var val = content.UserInfo[key];
                                data[k.ToString()] = val?.ToString() ?? "";
                            }
                        }
                    }
                    
                    var pushData = new PushNotificationData(
                        content.Title ?? "",
                        content.Body ?? "",
                        "",
                        "",
                        data
                    );
                    
                    customizer.Customize(notification, content, pushData);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppAmbit] Error in customizer: {ex}");
                }
            };
            
            // Create ObjC block using BlockLiteral in unmanaged memory
            unsafe
            {
                // Allocate BlockLiteral in unmanaged memory
                _customizerBlockPtr = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
                BlockLiteral* blockPtr = (BlockLiteral*)_customizerBlockPtr;
                
                blockPtr->SetupBlockUnsafe(NotificationCustomizerTrampoline, _customizerAction);
                
                objc_msgSend_IntPtr(_pushNotificationsClassHandle, _selSetNotificationCustomizer, _customizerBlockPtr);
            }
        }
        else
        {
            _customizer = null;
            _customizerAction = null;
            objc_msgSend_IntPtr(_pushNotificationsClassHandle, _selSetNotificationCustomizer, IntPtr.Zero);
        }
    }

    [ObjCRuntime.MonoPInvokeCallback(typeof(Action<IntPtr, IntPtr>))]
    private static void NotificationCustomizerTrampoline(IntPtr block, IntPtr notificationPtr)
    {
        try
        {
            var action = BlockLiteral.GetTarget<Action<UNNotification>>(block);
            if (action != null && notificationPtr != IntPtr.Zero)
            {
                var notification = Runtime.GetNSObject<UNNotification>(notificationPtr);
                if (notification != null)
                {
                    action(notification);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit] Error in trampoline: {ex}");
        }
    }

    // --- Internal Notification Customizer ---
    [Register("NotificationCustomizerImpl")]
    private class NotificationCustomizerImpl : NSObject
    {
        private readonly PushNotifications.INotificationCustomizer _managed;

        public NotificationCustomizerImpl(PushNotifications.INotificationCustomizer managed)
        {
            _managed = managed;
        }

        // Protocol: @objc func customizeNotification(_ notification: UNMutableNotificationContent, data: [String: Any])
        [Export("customizeNotification:data:")]
        public void CustomizeNotification(UNMutableNotificationContent notification, NSDictionary data)
        {
            // Convert NSDictionary to Dictionary<string, object>
            var dict = new System.Collections.Generic.Dictionary<string, object>();
            if (data != null)
            {
                foreach (var key in data.Keys)
                {
                    if (key is NSString k)
                    {
                        var val = data[key];
                         // Basic conversion - can be improved for nested types if needed
                        dict[k.ToString()] = val.ToString() ?? ""; 
                    }
                }
            }
            
            // Reconstruct PushNotificationData
            var pushData = new PushNotificationData(
                notification.Title, 
                notification.Body, 
                "", // Color not applicable
                "", // Icon not applicable
                dict
            );

            // Pass 'notification' as context equivalent (user can modify it)
            _managed.Customize(notification, notification, pushData); 
        }
    }

    // --- Internal Token Listener ---
    [Register("TokenListenerImpl")]
    [Preserve(AllMembers = true)]
    internal class TokenListenerImpl : NSObject
    {
        [Export("onNewToken:")]
        public void OnNewToken(NSString token)
        {
            var tokenStr = token.ToString();
            _lastPushToken = tokenStr;
            
            // LOG TOKEN AS REQUESTED
            Console.WriteLine($"{LogTag}: (C#) Received Token: {tokenStr}");

            _ = Task.Run(async () =>
            {
                // Always sync token if received - let backend handle logic
                try {
                    // Use actual notification state instead of hardcoded true
                    var isEnabled = PushNotificationsIos.IsNotificationsEnabled();
                    Console.WriteLine($"{LogTag}: (C#) Syncing token. Enabled? {isEnabled}");
                    await AppAmbitSdk.UpdateConsumerAsync(tokenStr, isEnabled);
                    Console.WriteLine($"{LogTag}: (C#) Token synced.");
                } catch (Exception ex) {
                        Console.WriteLine($"{LogTag}: Error syncing token: {ex.Message}");
                }
            });
        }
    }

    // --- P/Invokes ---
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool value);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlerror();
}


#endif
