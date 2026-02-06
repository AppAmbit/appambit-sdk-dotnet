using AppAmbit;
using AppAmbitMaui;
namespace AppAmbitTestingMacOs;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		AppAmbitMaui.AppAmbitSdk.Start("<YOUR-APPKEY>");

		RemoteConfig.SetDefaults(new Dictionary<string, object>
		{
			{ "banner", true },
			{ "data", "If you can see this message you are using local values" },
			{ "discount", 8 },
			{ "max_upload", 15.6 }
		});

        _ = Task.Run(async () => await RemoteConfig.FetchAndActivate());

		return true;
	}

	public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
	{
		// Called when a new scene session is being created.
		// Use this method to select a configuration to create the new scene with.
		// "Default Configuration" is defined in the Info.plist's 'UISceneConfigurationName' key.
		return new UISceneConfiguration("Default Configuration", connectingSceneSession.Role);
	}

	public override void DidDiscardSceneSessions(UIApplication application, NSSet<UISceneSession> sceneSessions)
	{
		// Called when the user discards a scene session.
		// If any sessions were discarded while the application was not running, this will be called shortly after 'FinishedLaunching'.
		// Use this method to release any resources that were specific to the discarded scenes, as they will not return.
	}
}
