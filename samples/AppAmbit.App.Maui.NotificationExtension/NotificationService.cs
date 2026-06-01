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
    public override void DidReceiveNotificationRequest(
        UNNotificationRequest request,
        Action<UNNotificationContent> contentHandler)
    {
        if (request.Content.MutableCopy() is not UNMutableNotificationContent bestAttemptContent)
        {
            Log("[AppAmbit NSE] Failed to create mutable content copy.");
            contentHandler(request.Content);
            return;
        }

        Log($"[AppAmbit NSE] Processing notification -> {bestAttemptContent.Title}");

        var userInfo = bestAttemptContent.UserInfo;
        var dataPayload = userInfo[(NSString)"data"] as NSDictionary ?? userInfo;

        bestAttemptContent.Title += " Customs";

        if (dataPayload[(NSString)"category_type"] is NSString category)
            bestAttemptContent.CategoryIdentifier = category;

        if (dataPayload[(NSString)"chat_id"] is NSString threadId)
            bestAttemptContent.ThreadIdentifier = threadId;

        if (OperatingSystem.IsIOSVersionAtLeast(15)
            && dataPayload[(NSString)"is_urgent"] is NSString urgent
            && urgent == "true")
        {
            bestAttemptContent.InterruptionLevel = UNNotificationInterruptionLevel.TimeSensitive;
        }

        if (dataPayload[(NSString)"badge_count"] is NSString badgeStr && int.TryParse(badgeStr, out var badge))
            bestAttemptContent.Badge = badge;

        var newRequest = UNNotificationRequest.FromIdentifier(request.Identifier, bestAttemptContent, request.Trigger);

        Log("[AppAmbit NSE] Content modified, delegating to base.");
        base.DidReceiveNotificationRequest(newRequest, contentHandler);
    }

    protected override void HandlePayload(AppAmbitNotificationData notification, UNMutableNotificationContent content)
    {
        Log($"[AppAmbit NSE] HandlePayload — title: {notification.Title}, body: {notification.Body}");
    }

    protected override void OnTimeExpiring()
    {
        Log("[AppAmbit NSE] Time limit reached — delivering best attempt content");
    }

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
