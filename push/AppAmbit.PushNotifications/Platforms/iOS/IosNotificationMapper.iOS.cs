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

        var data = new Dictionary<string, string>();
        foreach (var key in userInfo.Keys)
        {
            if (key is NSString k && key.ToString() != "aps")
            {
                var value = userInfo[key];
                if (value != null) data[k.ToString()] = value.ToString();
            }
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
