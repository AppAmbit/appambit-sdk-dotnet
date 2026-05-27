using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AppAmbit.PushNotifications;

namespace AppAmbitTestingApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Register push listeners before MAUI UI is created so they are active
        // when a notification arrives while the app is in background or killed.
        PushNotifications.SetForegroundListener(data =>
            Console.WriteLine($"[AppAmbitMaui] Foreground push: {data.Title}"));

        PushNotifications.SetOpenedListener(data =>
            Console.WriteLine($"[AppAmbitMaui] Opened push: {data.Title}"));

        PushNotifications.Android.SetBackgroundListener(data =>
            Console.WriteLine($"[AppAmbitMaui] Background push: {data.Title}"));

        PushNotifications.Android.SetNotificationCustomizer(new AppAmbitNotificationCustomizer());

        base.OnCreate(savedInstanceState);
        PushNotifications.Start(this);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        PushNotifications.Android.HandleNotificationOpened(intent);
    }
}
