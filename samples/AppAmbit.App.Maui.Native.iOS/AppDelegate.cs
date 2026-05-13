using AppAmbit.PushNotifications;
using AppAmbit;
namespace AppAmbitTestingiOS;


[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
	public override UIWindow? Window
	{
		get;
		set;
	}

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        PushNotifications.SetForegroundListener(data =>
            Console.WriteLine($"[AppAmbitNativeiOS] Foreground push: {data.Title}"));

        PushNotifications.SetOpenedListener(data =>
            Console.WriteLine($"[AppAmbitNativeiOS] Opened push: {data.Title}"));

        AppAmbit.RemoteConfig.Enable();
        AppAmbitSdk.Start("<YOUR-APPKEY>");
        PushNotifications.Start();

        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new MainTabBarController();
        Window.MakeKeyAndVisible();
        return true;
    }
}
