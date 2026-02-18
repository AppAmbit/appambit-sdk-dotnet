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
            .UseAppAmbit("17bc9724-ce70-470a-aed7-d6b902b7c262");

        

        return builder.Build();
    }
}
    