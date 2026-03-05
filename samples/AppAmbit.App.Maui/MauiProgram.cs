using AppAmbitMaui;
using AppAmbit;

namespace AppAmbitTestingApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        RemoteConfig.Enable();
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseAppAmbit("<YOUR-APPKEY>");

        

        return builder.Build();
    }
}
    