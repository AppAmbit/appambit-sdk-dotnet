#if IOS
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using ObjCRuntime;
using Foundation;
using AppAmbit;
using UserNotifications;

namespace AppAmbit.PushNotifications;

[SupportedOSPlatform("ios12.0")]
internal static class PushNotificationsIos
{
    // Point back to the mechanial PushKernel class
    private const string NativeClassName = "PushKernel";
    private static readonly IntPtr _classHandle = Class.GetHandle(NativeClassName);
    
    // Selectors for PushKernel
    private static readonly IntPtr _selSetDebugMode = Selector.GetHandle("setDebugMode:");
    private static readonly IntPtr _selSetupSwizzling = Selector.GetHandle("setupSwizzling");
    private static readonly IntPtr _selRequestNotificationPermission = Selector.GetHandle("requestNotificationPermissionWithListener:");
    private static readonly IntPtr _selSetNotificationsEnabled = Selector.GetHandle("setNotificationsEnabled:");
    private static readonly IntPtr _selIsNotificationsEnabled = Selector.GetHandle("isNotificationsEnabled");
    private static readonly IntPtr _selSetTokenListener = Selector.GetHandle("setTokenListener:");
    private static readonly IntPtr _selSetNotificationCustomizer = Selector.GetHandle("setNotificationCustomizer:");

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
            throw new PlatformNotSupportedException("AppAmbitPushNotifications native class 'PushKernel' is not available.");
        }
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

    public static void RequestNotificationPermission()
    {
        EnsureNativeAvailable();
        objc_msgSend_IntPtr(_classHandle, _selRequestNotificationPermission, IntPtr.Zero);
    }

    public static void SetNotificationCustomizer(PushNotifications.INotificationCustomizer? customizer)
    {
        // ... (Keep existing stub)
        objc_msgSend_IntPtr(_classHandle, _selSetNotificationCustomizer, IntPtr.Zero);
    }

    public static PushNotifications.INotificationCustomizer? GetNotificationCustomizer() => _customizer;

    // --- Internal Token Listener ---
    [Register("TokenListenerImpl")]
    private class TokenListenerImpl : NSObject
    {
        [Export("onNewToken:")]
        public void OnNewToken(NSString token)
        {
            var tokenStr = token.ToString();
            _lastPushToken = tokenStr;
            
             _ = Task.Run(async () =>
            {
                // Check if enabled before syncing
                if (IsNotificationsEnabled())
                {
                    Console.WriteLine($"{LogTag}: (C#) Syncing token...");
                    try {
                        await AppAmbitSdk.UpdateConsumerAsync(tokenStr, true);
                        Console.WriteLine($"{LogTag}: (C#) Token synced.");
                    } catch (Exception ex) {
                         Console.WriteLine($"{LogTag}: Error syncing token: {ex.Message}");
                    }
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
}
#endif
