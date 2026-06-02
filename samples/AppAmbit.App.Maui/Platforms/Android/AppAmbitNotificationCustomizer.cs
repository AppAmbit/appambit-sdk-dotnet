using System.Collections.Generic;
using AppAmbit.PushNotifications;

namespace AppAmbitTestingApp;

internal sealed class AppAmbitNotificationCustomizer : PushNotifications.INotificationCustomizer
{
    public void Customize(object context, object builder, PushNotificationData notification)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[AppAmbitMaui][Customizer] title='{notification.Title}' " +
            $"ticker='{notification.Android?.Ticker}' sticky={notification.Android?.Sticky} " +
            $"visibility='{notification.Android?.Visibility}' priority='{notification.Android?.Priority}' " +
            $"channel='{notification.Android?.ChannelId}' clickAction='{notification.Android?.ClickAction}' " +
            $"data={{{string.Join(", ", notification.Data ?? new Dictionary<string, string>())}}}");

        dynamic b = builder;

        // Append a marker to the notification.
        b.SetSubText("via AppAmbit");

        // Group related notifications if the payload includes a group_key data field.
        if (notification.Data is { } data && data.TryGetValue("group_key", out var groupKey))
        {
            b.SetGroup(groupKey);
        }
    }
}
