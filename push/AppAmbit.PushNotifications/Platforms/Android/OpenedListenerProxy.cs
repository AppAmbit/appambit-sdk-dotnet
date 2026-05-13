namespace AppAmbit.PushNotifications;

public sealed class OpenedListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.IOpenedNotificationListener
{
    private readonly System.Action<PushNotificationData> _listener;

    public OpenedListenerProxy() { _listener = _ => { }; }
    public OpenedListenerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { _listener = _ => { }; }

    public OpenedListenerProxy(System.Action<PushNotificationData> listener)
    {
        _listener = listener;
    }

    public void OnOpenedNotification(Com.Appambit.Sdk.Models.AppAmbitNotification notification)
    {
        _listener(AndroidNotificationMapper.ToData(notification));
    }
}
