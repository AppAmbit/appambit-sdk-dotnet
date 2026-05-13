namespace AppAmbit.PushNotifications;

public sealed class ForegroundListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.IForegroundNotificationListener
{
    private readonly System.Action<PushNotificationData> _listener;

    public ForegroundListenerProxy() { _listener = _ => { }; }
    public ForegroundListenerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { _listener = _ => { }; }

    public ForegroundListenerProxy(System.Action<PushNotificationData> listener)
    {
        _listener = listener;
    }

    public void OnForegroundNotificationReceived(Com.Appambit.Sdk.Models.AppAmbitNotification notification)
    {
        _listener(AndroidNotificationMapper.ToData(notification));
    }
}
