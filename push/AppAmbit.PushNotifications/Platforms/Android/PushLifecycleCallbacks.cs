namespace AppAmbit.PushNotifications;

public sealed class PushLifecycleCallbacks : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    public PushLifecycleCallbacks() { }
    public PushLifecycleCallbacks(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public void OnActivityCreated(Android.App.Activity activity, Android.OS.Bundle? savedInstanceState)
    {
        PushNotificationsAndroid.SetCurrentActivity(activity);
        PushNotificationsAndroid.TryHandleOpenedIntent(activity.Intent);
    }

    public void OnActivityResumed(Android.App.Activity activity)
    {
        PushNotificationsAndroid.SetCurrentActivity(activity);
        PushNotificationsAndroid.TryHandleOpenedIntent(activity.Intent);
    }
    public void OnActivityDestroyed(Android.App.Activity activity) => PushNotificationsAndroid.ClearCurrentActivity(activity);
    public void OnActivityPaused(Android.App.Activity activity) { }
    public void OnActivitySaveInstanceState(Android.App.Activity activity, Android.OS.Bundle outState) { }
    public void OnActivityStarted(Android.App.Activity activity) { }
    public void OnActivityStopped(Android.App.Activity activity) { }
}
