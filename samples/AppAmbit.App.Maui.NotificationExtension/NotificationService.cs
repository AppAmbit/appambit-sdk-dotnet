#if IOS
using Foundation;
using ObjCRuntime;
using System.Runtime.InteropServices;
using UserNotifications;
using AppAmbit.PushNotifications;

namespace AppAmbit.App.Maui.NotificationExtension;

[Register("NotificationService")]
public class SampleNotificationService : AppAmbitNotificationServiceExtension
{
    protected override void OnNotificationArrived(UNNotificationRequest request)
    {
        Log($"[AppAmbit NSE] Notification arrived — identifier: {request.Identifier}");
    }

    protected override void HandlePayload(AppAmbitNotificationData notification, UNMutableNotificationContent content)
    {
        content.Title = (notification.Title ?? content.Title) + " + Custom";
        Log($"[AppAmbit NSE] title: {notification.Title}, body: {notification.Body}");
    }

    protected override void OnTimeExpiring()
    {
        Log("[AppAmbit NSE] Time limit reached — delivering best attempt content");
    }

    // Writes to the iOS Unified Logging System — visible in Console.app on Mac,
    // filtered by this extension's process name.
    private static void Log(string message)
    {
        var ptr = NSString.CreateNative(message);
        NSLogNative(ptr);
        NSString.ReleaseNative(ptr);
    }

    [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "NSLog")]
    private static extern void NSLogNative(IntPtr format);
}
#endif
