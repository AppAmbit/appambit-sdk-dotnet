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
        AppAmbit.RemoteConfig.Enable();
        AppAmbitSdk.Start("581a777b-4c6f-4290-93db-834c08c97e37");
        PushNotifications.Start();

        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new MainTabBarController();
        Window.MakeKeyAndVisible();
        return true;
    }
}
