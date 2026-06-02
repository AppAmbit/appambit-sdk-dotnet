using Foundation;
using UIKit;
using AppAmbit.PushNotifications;

namespace AppAmbitTestingApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        var result = base.FinishedLaunching(app, options);

        // Start push and wire token updates to AppAmbit (same pattern as Android)
        PushNotifications.Start(null);

        // Cold-start tap: when the app is fully terminated, iOS delivers the payload in the
        // launch options instead of the notification-center delegate. Hand it to the SDK so
        // it is buffered and delivered to the opened listener once that listener registers.
        PushNotifications.HandleLaunchOptions(options);

        return result;
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlerror();
}