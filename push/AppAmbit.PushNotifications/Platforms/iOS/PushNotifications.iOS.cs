#if IOS
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using ObjCRuntime;
using Foundation;
using AppAmbit;

namespace AppAmbit.PushNotifications;

[SupportedOSPlatform("ios12.0")]
internal static class PushNotificationsIos
{
    private const string NativeClassName = "PushNotifications";
    private static readonly IntPtr _classHandle = Class.GetHandle(NativeClassName);
    private static readonly IntPtr _selStart = Selector.GetHandle("start");
    private static readonly IntPtr _selSetNotificationsEnabled = Selector.GetHandle("setNotificationsEnabled:completion:");
    private static readonly IntPtr _selIsNotificationsEnabled = Selector.GetHandle("isNotificationsEnabled");
    private static readonly IntPtr _selRequestNotificationPermission = Selector.GetHandle("requestNotificationPermissionWithListener:");
    private static readonly IntPtr _selSetNotificationCustomizer = Selector.GetHandle("setNotificationCustomizer:");

    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;
    private static PushNotifications.INotificationCustomizer? _customizer;

    private static void EnsureNativeAvailable()
    {
        if (_classHandle == IntPtr.Zero)
        {
            throw new PlatformNotSupportedException("AppAmbitPushNotifications native class is not available.");
        }
    }

    public static void Start()
    {
        EnsureNativeAvailable();

        if (!_initialized)
        {
            // Assume pod handles token interception automatically
            _initialized = true;
        }

        objc_msgSend(_classHandle, _selStart);
    }

    public static void SetNotificationsEnabled(bool enabled)
    {
        EnsureNativeAvailable();

        // Create a completion block that does nothing
        var completionBlock = new BlockLiteral();
        completionBlock.SetupBlock((BlockLiteral block, bool success) =>
        {
            // Log or handle completion if needed
            Console.WriteLine($"{LogTag}: SetNotificationsEnabled completion: {success}");
        }, null);

        objc_msgSend_IntPtr(_classHandle, _selSetNotificationsEnabled, enabled ? 1 : 0, (IntPtr)completionBlock.Target);

        var token = _lastPushToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await AppAmbitSdk.UpdateConsumerAsync(token, enabled);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{LogTag}: Failed to sync consumer push state (enabled={enabled}): {ex}");
                }
                finally
                {
                    if (!enabled)
                    {
                        _lastPushToken = null;
                    }
                }
            });
        }
        else if (!enabled)
        {
            _lastPushToken = null;
        }
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
        _customizer = customizer;

        if (customizer == null)
        {
            objc_msgSend_IntPtr(_classHandle, _selSetNotificationCustomizer, IntPtr.Zero);
            return;
        }

        // Create a block for the customizer
        var customizerBlock = new BlockLiteral();
        customizerBlock.SetupBlock((BlockLiteral block, IntPtr contentPtr, IntPtr notificationPtr) =>
        {
            // Map to C# objects - this is simplified, need proper mapping
            // Assume content is UNMutableNotificationContent, notification is AppAmbitNotification
            // For now, call customizer with nulls or placeholders
            if (_customizer != null)
            {
                // TODO: Map contentPtr and notificationPtr to objects
                _customizer.Customize(null, null, new PushNotificationData("", "", "", "", null)); // Placeholder
            }
        }, null);

        objc_msgSend_IntPtr(_classHandle, _selSetNotificationCustomizer, (IntPtr)customizerBlock.Target);
    }

    public static PushNotifications.INotificationCustomizer? GetNotificationCustomizer()
    {
        return _customizer;
    }

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
    private static extern void objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, int value, IntPtr blockPtr);
}
#endif
