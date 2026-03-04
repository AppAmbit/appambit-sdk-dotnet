using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using AppAmbitAvalonia;
using AppAmbit.PushNotifications;

namespace AppAmbitTestingAppAvalonia.Android;

[Activity(
    Label = "AppAmbitTestingAppAvalonia.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AppAmbitSdk.Start("<YOUR_APPKEY>");
        PushNotifications.Start(this);
        
        return base.CustomizeAppBuilder(builder);
    }
}
