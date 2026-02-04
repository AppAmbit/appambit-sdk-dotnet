using AppAmbitMaui;
using AppAmbit.PushNotifications.Hosting;

namespace AppAmbitTestingApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        //Uncomment the line for automatic session management
        //Analytics.EnableManualSession();
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseAppAmbit("69232e5a-4797-471f-92d9-d5025fdcf91f")
            .UseAppAmbitPush();

        

        return builder.Build();
    }
}
    