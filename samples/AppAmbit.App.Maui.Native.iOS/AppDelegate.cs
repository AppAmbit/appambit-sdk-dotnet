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
        {
            Console.WriteLine($"[AppAmbitNativeiOS] Opened push: {data.Title}");
            Foundation.NSThread.MainThread.BeginInvokeOnMainThread(() =>
            {
                var nav = (Window?.RootViewController as UITabBarController)?.SelectedViewController as UINavigationController;
                nav?.PushViewController(new SecondView(), true);
            });
        });

        AppAmbit.RemoteConfig.Enable();
        AppAmbitSdk.Start("<YOUR_APPKEY>");
        PushNotifications.Start();
        PushNotifications.HandleLaunchOptions(launchOptions);

        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new MainTabBarController();
        Window.MakeKeyAndVisible();
        return true;
    }
}
