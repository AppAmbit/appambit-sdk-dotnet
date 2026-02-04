using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Hosting;
using System.Diagnostics;
using AppAmbit.PushNotifications;

namespace AppAmbit.PushNotifications.Hosting;

public static class AppAmbitPushExtensions
{
    public static MauiAppBuilder UseAppAmbitPush(this MauiAppBuilder builder, bool enableNotifications = true)
    {
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android =>
            {
                // Core SDK initializes in OnCreate, so we can hook OnCreate too (chained) 
                // or OnStart. OnCreate is safest to match Core's immediate init.
                // Since hooks assume order, if this is called after UseAppAmbit, it should be fine.
                android.OnCreate((activity, state) => 
                {
                    try 
                    {
                        PushNotifications.Start(activity, enableNotifications);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AppAmbitPush] Error starting Push SDK: {ex}");
                    }
                });
            });
#elif IOS
            events.AddiOS(ios =>
            {
                ios.FinishedLaunching((application, options) =>
                {
                    try 
                    {
                        PushNotifications.Start(null, enableNotifications);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AppAmbitPush] Error starting Push SDK: {ex}");
                    }
                    return true;
                });
            });
#endif
        });

        return builder;
    }
}
