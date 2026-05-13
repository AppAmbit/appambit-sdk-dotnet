namespace AppAmbit.PushNotifications;

public sealed class BackgroundListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.IBackgroundNotificationListener
{
    private readonly System.Action<PushNotificationData> _listener;

    public BackgroundListenerProxy() { _listener = _ => { }; }
    public BackgroundListenerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { _listener = _ => { }; }

    public BackgroundListenerProxy(System.Action<PushNotificationData> listener)
    {
        _listener = listener;
    }

    public void OnBackgroundNotificationReceived(Com.Appambit.Sdk.Models.AppAmbitNotification notification)
    {
        _listener(AndroidNotificationMapper.ToData(notification));
    }
}
