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
        AppAmbitSdk.Start("581a777b-4c6f-4290-93db-834c08c97e37");
        PushNotifications.Start(this);
        
        return base.CustomizeAppBuilder(builder);
    }
}
