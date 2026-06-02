namespace AppAmbit.PushNotifications;

public sealed class NotificationCustomizerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.INotificationCustomizer
{
    private readonly PushNotifications.INotificationCustomizer? _managed;

    public NotificationCustomizerProxy() { }
    public NotificationCustomizerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public NotificationCustomizerProxy(PushNotifications.INotificationCustomizer managed)
    {
        _managed = managed;
    }

    public void Customize(Android.Content.Context context, AndroidX.Core.App.NotificationCompat.Builder builder, Com.Appambit.Sdk.Models.AppAmbitNotification notification)
    {
        if (_managed == null) return;
        _managed.Customize(
            (object)context,
            (object)builder,
            AndroidNotificationMapper.ToData(notification));
    }
}
