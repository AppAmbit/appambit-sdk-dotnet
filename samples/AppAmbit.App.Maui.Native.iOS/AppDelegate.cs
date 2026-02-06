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
        try
        {
            AppAmbitSdk.Start("<YOUR-APPKEY>");
            try
            {
                PushNotifications.Start();
            }
            catch (System.Exception exPush)
            {
                Console.WriteLine($"PushNotifications.Start() failed: {exPush}");
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"AppAmbitSdk.Start() failed: {ex}");
        }

        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new MainTabBarController();
        Window.MakeKeyAndVisible();
        return true;
    }
}
