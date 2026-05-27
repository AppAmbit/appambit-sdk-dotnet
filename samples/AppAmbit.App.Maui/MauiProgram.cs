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
            .UseAppAmbit("b55c1566-955d-456c-8ac5-3edd907b59a1"); //android: 67c5b287-ebc8-4560-afe4-ec5c774e6145, ios:94c60591-c195-4b69-b72f-a4b6f4dda908

        

        return builder.Build();
    }
}
    