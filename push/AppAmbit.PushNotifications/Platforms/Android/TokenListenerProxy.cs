using Android.Util;

namespace AppAmbit.PushNotifications;

public sealed class TokenListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.ITokenListener
{
    public TokenListenerProxy() { }
    public TokenListenerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public TokenListenerProxy(Android.Content.Context context) { }

    public void OnNewToken(string token)
    {
        PushNotificationsAndroid._lastPushToken = token;
        Log.Debug(PushNotificationsAndroid.LogTag, $"FCM token cached: {token.Substring(0, System.Math.Min(10, token.Length))}...");
        // Only sync to backend when the system has granted notification permission.
        // If permission is not granted the token is cached in _lastPushToken and will be sent
        // when the user grants permission and SetNotificationsEnabled(true) is called.
        if (PushNotificationsAndroid.HasNotificationPermission())
        {
            _ = System.Threading.Tasks.Task.Run(() =>
                AppAmbitSdk.UpdateConsumerAsync(token, true));
        }
    }
}
