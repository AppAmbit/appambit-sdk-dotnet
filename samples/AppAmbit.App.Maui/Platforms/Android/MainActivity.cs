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
        // SetOpenedListener is intentionally NOT registered here.
        // Registering it before MAUI/Shell is ready would consume the cold-start intent
        // before MainPage can register the listener that actually navigates.
        // MainPage registers all three listeners (foreground, opened, background).

        PushNotifications.Android.SetNotificationCustomizer(new AppAmbitNotificationCustomizer());

        base.OnCreate(savedInstanceState);
        PushNotifications.Start(this);

        var bottomNavigation = new MauiBottomNavigationView(this);
        var layoutParams = new Android.Widget.FrameLayout.LayoutParams(
            Android.Views.ViewGroup.LayoutParams.MatchParent,
            MauiBottomNavigationView.HeightInDp(this))
        {
            Gravity = Android.Views.GravityFlags.Bottom
        };
        Window?.AddContentView(bottomNavigation, layoutParams);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        PushNotifications.Android.HandleNotificationOpened(intent);
    }
}
