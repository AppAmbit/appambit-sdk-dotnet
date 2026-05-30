#if IOS
using System.Collections.Generic;
using Foundation;

namespace AppAmbit.PushNotifications;

internal static class IosNotificationMapper
{
    public static PushNotificationData ToData(NSDictionary userInfo)
    {
        var aps   = (userInfo["aps"] as NSDictionary) ?? userInfo;
        var alert = aps["alert"] as NSDictionary;

        string? title    = (alert?["title"]    ?? userInfo["title"])    as NSString;
        string? body     = (alert?["body"]     ?? userInfo["body"])     as NSString;
        string? imageUrl = userInfo["image"] as NSString;

        string? sound    = aps["sound"] as NSString;
        int?    badge    = aps["badge"] is NSNumber n ? (int?)n.Int32Value : null;
        string? threadId = aps["thread-id"] as NSString;
        string? category = aps["category"] as NSString;

        // Avoid calling .ToString() on NSObject values: for non-NSString values (e.g. NSDictionary)
        // .ToString() returns the managed type name instead of the actual content.
        // Cast explicitly to NSString so only proper string values are included.
        var data = new Dictionary<string, string>();
        foreach (var key in userInfo.Keys)
        {
            if (key is not NSString k) continue;
            var keyStr = (string)k;
            if (keyStr == "aps") continue;
            if (userInfo[key] is NSString strValue)
                data[keyStr] = (string)strValue;
        }

        return new PushNotificationData(
            Title: title,
            Body: body,
            ImageUrl: imageUrl,
            Data: data,
            Ios: new IosPushData(sound, badge, threadId, category));
    }
}
#endif
