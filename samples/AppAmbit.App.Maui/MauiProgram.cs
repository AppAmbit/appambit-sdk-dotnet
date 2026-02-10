using AppAmbitMaui;

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
            .UseAppAmbit("f0bdde14-fafc-4f2b-8a71-f0ffdf76bd03");

        

        return builder.Build();
    }
}
    