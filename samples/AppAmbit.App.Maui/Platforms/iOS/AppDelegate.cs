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
        try
        {
            var bundlePath = NSBundle.MainBundle.BundlePath;
            var pushPath = Path.Combine(bundlePath, "Frameworks", "AppAmbitPushNotifications.framework", "AppAmbitPushNotifications");
            
            // 2. Load Push
             if (ObjCRuntime.Dlfcn.dlopen(pushPath, 0) == IntPtr.Zero)
            {
                 Console.WriteLine($"[AppAmbit] ERROR: Failed to load Push framework. Error: {System.Runtime.InteropServices.Marshal.PtrToStringAnsi(dlerror())}");
            }
            
            // 3. Start SDK
            PushNotifications.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppAmbit] FATAL: Error initializing PushNotifications: {ex}");
        }
        return base.FinishedLaunching(app, options);
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlerror();
}