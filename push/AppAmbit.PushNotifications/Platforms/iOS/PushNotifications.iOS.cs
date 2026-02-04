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

namespace AppAmbit.PushNotifications;

[SupportedOSPlatform("ios12.0")]
internal static class PushNotificationsIos
{
    // Point back to the mechanial PushKernel class
    private const string NativeClassName = "PushKernel";
    private static IntPtr _classHandle;
    
    // Selectors for PushKernel
    private static IntPtr _selSetDebugMode;
    private static IntPtr _selSetupSwizzling;
    private static IntPtr _selRequestNotificationPermission;
    private static IntPtr _selSetNotificationsEnabled;
    private static IntPtr _selIsNotificationsEnabled;
    private static IntPtr _selSetTokenListener;
    private static IntPtr _selSetNotificationCustomizer;

    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;
    private static PushNotifications.INotificationCustomizer? _customizer;
    
    // Hold reference to listener to prevent GC
    private static object? _tokenListener;

    private static void EnsureNativeAvailable()
    {
        if (_classHandle == IntPtr.Zero)
        {
            LoadNativeFrameworks();
            _classHandle = Class.GetHandle(NativeClassName);
            
            if (_classHandle == IntPtr.Zero)
            {
                 throw new PlatformNotSupportedException("AppAmbitPushNotifications native class 'PushKernel' is not available. Native frameworks may be missing or failed to load.");
            }
            
            InitializeSelectors();
        }
    }

    private static void LoadNativeFrameworks()
    {
        try
        {
            var bundlePath = NSBundle.MainBundle.BundlePath;
            var sdkPath = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbitSdk.framework", "AppAmbitSdk");
            var pushPath = System.IO.Path.Combine(bundlePath, "Frameworks", "AppAmbitPushNotifications.framework", "AppAmbitPushNotifications");

            // 1. Load Sdk
            if (dlopen(sdkPath, 0) == IntPtr.Zero)
            {
                 Debug.WriteLine($"[AppAmbit] ERROR: Failed to load Sdk framework. Error: {Marshal.PtrToStringAnsi(dlerror())}");
            }

            // 2. Load Push
             if (dlopen(pushPath, 0) == IntPtr.Zero)
            {
                 Debug.WriteLine($"[AppAmbit] ERROR: Failed to load Push framework. Error: {Marshal.PtrToStringAnsi(dlerror())}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit] FATAL: Error loading native frameworks: {ex}");
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

        // 3. Setup Swizzling
        objc_msgSend(_classHandle, _selSetupSwizzling);        
    }

    public static void SetNotificationsEnabled(bool enabled)
    {
        EnsureNativeAvailable();
        objc_msgSend_bool(_classHandle, _selSetNotificationsEnabled, enabled);

        // Update consumer logic
        var token = _lastPushToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _ = Task.Run(async () =>
            {
                try { await AppAmbitSdk.UpdateConsumerAsync(token, enabled); }
                catch (Exception ex) { Console.WriteLine($"{LogTag}: Sync error: {ex}"); }
                finally { if (!enabled) _lastPushToken = null; }
            });
        }
        else if (!enabled) { _lastPushToken = null; }
    }

    public static bool IsNotificationsEnabled()
    {
        EnsureNativeAvailable();
        return objc_msgSend_bool_ret(_classHandle, _selIsNotificationsEnabled);
    }

    public static bool HasSystemPermission()
    {
        EnsureNativeAvailable();
        // Check iOS notification authorization status
        var center = UNUserNotificationCenter.Current;
        var tcs = new TaskCompletionSource<bool>();
        
        center.GetNotificationSettings(settings =>
        {
            var authorized = settings.AuthorizationStatus == UNAuthorizationStatus.Authorized || 
                           settings.AuthorizationStatus == UNAuthorizationStatus.Provisional;
            tcs.SetResult(authorized);
        });
        
        return tcs.Task.Result;
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

    public static void RequestNotificationPermission(Action<bool>? callback)
    {
        EnsureNativeAvailable();
        
        NSObject? listenerInfo = null;
        if (callback != null)
        {
            var listener = new PermissionListenerImpl(callback);
            listenerInfo = listener; // Keep alive if needed, or pass directly
            objc_msgSend_IntPtr(_classHandle, _selRequestNotificationPermission, listener.Handle);
        }
        else
        {
            objc_msgSend_IntPtr(_classHandle, _selRequestNotificationPermission, IntPtr.Zero);
        }
    }

    public static void SetNotificationCustomizer(PushNotifications.INotificationCustomizer? customizer)
    {
        EnsureNativeAvailable();
        if (customizer != null)
        {
            var proxy = new NotificationCustomizerImpl(customizer);
            _customizer = customizer; // Keep C# ref
            objc_msgSend_IntPtr(_classHandle, _selSetNotificationCustomizer, proxy.Handle);
        }
        else
        {
            _customizer = null;
            objc_msgSend_IntPtr(_classHandle, _selSetNotificationCustomizer, IntPtr.Zero);
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
    private class TokenListenerImpl : NSObject
    {
        [Export("onNewToken:")]
        public void OnNewToken(NSString token)
        {
            var tokenStr = token.ToString();
            _lastPushToken = tokenStr;
            
            // LOG TOKEN AS REQUESTED
            Debug.WriteLine($"{LogTag}: (C#) Received Token: {tokenStr}");

             _ = Task.Run(async () =>
            {
                // Check if enabled before syncing
                if (IsNotificationsEnabled())
                {
                    
                    try {
                        await AppAmbitSdk.UpdateConsumerAsync(tokenStr, true);
                        Debug.WriteLine($"{LogTag}: (C#) Token synced.");
                    } catch (Exception ex) {
                         Debug.WriteLine($"{LogTag}: Error syncing token: {ex.Message}");
                    }
                }
            });
        }
    }

    // --- Internal Permission Listener ---
    [Register("PermissionListenerImpl")]
    private class PermissionListenerImpl : NSObject
    {
        private readonly Action<bool> _callback;

        public PermissionListenerImpl(Action<bool> callback)
        {
            _callback = callback;
        }
        
        // Native Protocol: @objc func onPermissionResult(_ granted: Bool)
        [Export("onPermissionResult:")]
        public void OnPermissionResult(bool granted)
        {
            Debug.WriteLine($"{LogTag}: (C#) Permission Result: {granted}");
            // Marshal back to main thread if needed, usually callbacks are updated on UI thread
            MainThread.BeginInvokeOnMainThread(() => _callback(granted));
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
