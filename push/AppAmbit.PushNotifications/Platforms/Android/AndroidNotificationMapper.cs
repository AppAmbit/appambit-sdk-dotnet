using System.Collections.Generic;
using System.Linq;
using Com.Appambit.Sdk.Models;

namespace AppAmbit.PushNotifications;

internal static class AndroidNotificationMapper
{
    public static PushNotificationData ToData(AppAmbitNotification notification)
    {
        IDictionary<string, string> data = notification.Data is null
            ? new Dictionary<string, string>()
            : notification.Data.ToDictionary(kv => kv.Key, kv => kv.Value);

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
