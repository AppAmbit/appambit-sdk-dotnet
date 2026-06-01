using System.Collections.Generic;
using Com.Appambit.Sdk.Models;

namespace AppAmbit.PushNotifications;

internal static class AndroidNotificationMapper
{
    public static PushNotificationData ToData(AppAmbitNotification notification)
    {
        // Avoid LINQ .ToDictionary() on JavaDictionary<string,string>: its IEnumerable
        // path goes through Java's entrySet() iterator via JNI and drops entries whose
        // keys contain numeric characters (e.g. "k1", "data2"). Iterating .Keys and
        // doing indexed access uses a different JNI path that is reliable for all keys.
        var data = new Dictionary<string, string>();
        if (notification.Data is { } javaData)
        {
            foreach (var key in javaData.Keys)
            {
                if (key is not null && javaData.TryGetValue(key, out var value))
                    data[key] = value ?? string.Empty;
            }
        }

        return new PushNotificationData(
            Title: notification.Title,
            Body: notification.Body,
            ImageUrl: notification.ImageUrl,
            Data: data,
            Android: new AndroidPushData(
                Color:         notification.Color,
                SmallIconName: notification.SmallIconName,
                Ticker:        notification.Ticker,
                Sticky:        notification.Sticky?.BooleanValue(),
                Visibility:    notification.Visibility,
                ChannelId:     notification.ChannelId,
                Priority:      notification.Priority,
                Tag:           notification.Tag,
                Sound:         notification.Sound,
                ClickAction:   notification.ClickAction));
    }
}
