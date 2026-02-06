using AppAmbitMaui;
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
        AppAmbitSdk.Start("<YOUR-APPKEY>");
        AppAmbit.RemoteConfig.SetDefaults(new Dictionary<string, object>
        {
            { "banner", true },
            { "data", "If you can see this message you are using local values" },
            { "discount", 8 },
            { "max_upload", 15.6f }
        });
        
        System.Threading.Tasks.Task.Run(async () => await AppAmbit.RemoteConfig.FetchAndActivate());
        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        Window.RootViewController = new MainTabBarController();
        Window.MakeKeyAndVisible();        
        return true;
    }
}
