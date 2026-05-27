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
        _ = System.Threading.Tasks.Task.Run(() =>
            AppAmbitSdk.UpdateConsumerAsync(token, PushNotificationsAndroid.IsNotificationsEnabled()));
    }
}
