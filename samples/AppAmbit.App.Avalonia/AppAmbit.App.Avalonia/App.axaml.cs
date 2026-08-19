using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using AppAmbitTestingAppAvalonia.ViewModels;
using AppAmbitTestingAppAvalonia.Views;
using AppAmbitAvalonia;
using AppAmbit.PushNotifications;

namespace AppAmbitTestingAppAvalonia;

public partial class App : Avalonia.Application
{
#if ANDROID
    private const string AppKey = "294f7dd6-987e-493b-b13c-dfdfd0cdcd3e";
#elif IOS
    private const string AppKey = "e6174d4c-298b-4221-9a2d-1e913b912e25";
#else
    private const string AppKey = "<YOUR_APPKEY>";
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[AppAmbit] OnFrameworkInitializationCompleted called.");

        RemoteConfig.Enable();
        AppAmbitSdk.Start(AppKey);

        PushNotifications.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
