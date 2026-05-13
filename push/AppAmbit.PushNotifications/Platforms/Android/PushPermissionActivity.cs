using Android.App;
using Android.OS;
using Android.Util;

namespace AppAmbit.PushNotifications;

[Activity(
    Theme = "@android:style/Theme.Translucent.NoTitleBar",
    Exported = false,
    Name = "com.appambit.pushnotifications.PushPermissionActivity")]
public class PushPermissionActivity : AndroidX.Activity.ComponentActivity
{
    private const string Tag = "AppAmbitPushSDKNET";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Log.Debug(Tag, "PushPermissionActivity: OnCreate - registering ActivityResult launcher");

        try
        {
            var launcher = RegisterForActivityResult(
                new AndroidX.Activity.Result.Contract.ActivityResultContracts.RequestPermission(),
                new PermissionResultCallback(this));

            Log.Debug(Tag, "PushPermissionActivity: Launching permission request for POST_NOTIFICATIONS");
            launcher.Launch(Android.Manifest.Permission.PostNotifications);
        }
        catch (System.Exception ex)
        {
            Log.Error(Tag, $"PushPermissionActivity: Failed to launch permission request: {ex}");
            PushNotificationsAndroid.HandlePermissionResult(false);
            Finish();
        }
    }
}
