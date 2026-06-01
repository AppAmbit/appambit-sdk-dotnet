using Android.Util;

namespace AppAmbit.PushNotifications;

public sealed class PermissionResultCallback : Java.Lang.Object, AndroidX.Activity.Result.IActivityResultCallback
{
    private readonly PushPermissionActivity? _activity;

    public PermissionResultCallback() { }
    public PermissionResultCallback(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public PermissionResultCallback(PushPermissionActivity activity)
    {
        _activity = activity;
    }

    public void OnActivityResult(Java.Lang.Object? result)
    {
        var granted = result is Java.Lang.Boolean b && b.BooleanValue();
        Log.Debug("AppAmbitPushSDKNET", $"PushPermissionActivity: OnActivityResult - granted={granted}");
        PushNotificationsAndroid.HandlePermissionResult(granted);
        _activity?.Finish();
    }
}
