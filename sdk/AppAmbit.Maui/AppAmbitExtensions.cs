using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using Microsoft.Maui.LifecycleEvents;
using AppAmbit.Services;
using AppAmbit.Services.Interfaces;
using AppAmbit;

namespace AppAmbitMaui;
    
public static class AppAmbitMauiExtensions
{
    private static string? _lastPageName;
    private static bool _firstNavSkipped;
    private static Shell? _hookedShell;
    private static NavigationPage? _hookedNavPage;

    public static MauiAppBuilder UseAppAmbit(this MauiAppBuilder builder, string appKey)
    {
        AppAmbit.AppAmbitSdk.MarkConfiguredByBuilder();

        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android =>
            {
                android.OnCreate((activity, state) => { AppAmbit.AppAmbitSdk.StartFromHostLifecycle(appKey); HookPageNavigation(); });
                android.OnPause(activity => { AppAmbit.AppAmbitSdk.Sleep(); });
                android.OnResume(activity => { _ = AppAmbit.AppAmbitSdk.ResumeAsync(); HookPageNavigation(); });
                android.OnStop(activity => { AppAmbit.AppAmbitSdk.Sleep(); });
                android.OnRestart(activity => { _ = AppAmbit.AppAmbitSdk.ResumeAsync(); HookPageNavigation(); });
                android.OnDestroy(activity => { AppAmbit.AppAmbitSdk.End(); });
            });
#elif IOS
            events.AddiOS(ios =>
            {
                ios.FinishedLaunching((application, options) =>
                {
                    AppAmbit.AppAmbitSdk.StartFromHostLifecycle(appKey);
                    HookPageNavigation();
                    return true;
                });
                ios.DidEnterBackground(application => { AppAmbit.AppAmbitSdk.Sleep(); });
                ios.WillEnterForeground(application => { _ = AppAmbit.AppAmbitSdk.ResumeAsync(); HookPageNavigation(); });
                ios.WillTerminate(application => { AppAmbit.AppAmbitSdk.End(); });
            });
#endif
        });

        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;

        builder.Services.AddSingleton<IAPIService, APIService>();
        builder.Services.AddSingleton<IStorageService, StorageService>();
        builder.Services.AddSingleton<IAppInfoService, AppInfoService>();

        return builder;
    }

    private static async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        Debug.WriteLine("OnConnectivityChanged");
        if (e.NetworkAccess != NetworkAccess.Internet)
        {
            BreadcrumbManager.SaveFile(BreadcrumbsConstants.offline);
            return;
        }

        await AppAmbit.AppAmbitSdk.EnsureTokenAsync(null);

        BreadcrumbManager.LoadBreadcrumbsFromFile();
        await SessionManager.SendEndSessionFromDatabase();
        await SessionManager.SendStartSessionIfExist();
        await AppAmbit.Crashes.LoadCrashFileIfExists();        
        await BreadcrumbManager.AddAsync(BreadcrumbsConstants.online);
        await AppAmbit.AppAmbitSdk.SendPendingAsync();
    }

    private static void HookPageNavigation()
    {
        var app = Application.Current;
        if (app == null) return;

        app.PropertyChanged -= OnAppPropertyChanged;
        app.PropertyChanged += OnAppPropertyChanged;

        AttachToMainPage(GetCurrentPage(app));
    }

    private static void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Windows collection or current window/page may have changed — re-evaluate current page
        var app = sender as Application;
        AttachToMainPage(GetCurrentPage(app));
    }

    private static void AttachToMainPage(Page? page)
    {
        if (_hookedShell != null)
        {
            _hookedShell.Navigated -= OnShellNavigated;
            _hookedShell = null;
        }
        if (_hookedNavPage != null)
        {
            _hookedNavPage.Pushed -= OnNavPushed;
            _hookedNavPage.Popped -= OnNavPopped;
            _hookedNavPage = null;
        }

        if (page is Shell shell)
        {
            _hookedShell = shell;
            shell.Navigated += OnShellNavigated;
            _lastPageName = GetDisplayName(shell.CurrentPage);
            _firstNavSkipped = false;
            return;
        }

        if (page is NavigationPage nav)
        {
            _hookedNavPage = nav;
            nav.Pushed += OnNavPushed;
            nav.Popped += OnNavPopped;
            _lastPageName = GetDisplayName(nav.CurrentPage);
            _firstNavSkipped = false;
        }
        else
        {
            _lastPageName = GetDisplayName(page);
            _firstNavSkipped = false;
        }
    }

    private static async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var shell = sender as Shell;
        var currentName = GetDisplayName(shell?.CurrentPage);
        if (string.IsNullOrWhiteSpace(currentName)) return;

        if (!_firstNavSkipped)
        {
            _firstNavSkipped = true;
            _lastPageName = currentName;
            return;
        }

        var prev = _lastPageName;
        if (prev == null || prev == currentName)
        {
            _lastPageName = currentName;
            return;
        }

        await BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onDisappear}: {prev}");
        _lastPageName = currentName;
        await BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onAppear}: {currentName}");
    }

    private static async void OnNavPushed(object? sender, NavigationEventArgs e)
    {
        var newName = GetDisplayName(e?.Page);
        if (string.IsNullOrWhiteSpace(newName)) return;

        if (!_firstNavSkipped)
        {
            _firstNavSkipped = true;
            _lastPageName = newName;
            return;
        }

        var prev = _lastPageName;
        if (prev == newName)
        {
            _lastPageName = newName;
            return;
        }

        if (!string.IsNullOrWhiteSpace(prev))
            await BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onDisappear}: {prev}");

        _lastPageName = newName;
        await BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onAppear}: {newName}");
    }

    private static async void OnNavPopped(object? sender, NavigationEventArgs e)
    {
        var nav = sender as NavigationPage;
        var currentName = GetDisplayName(nav?.CurrentPage);
        if (string.IsNullOrWhiteSpace(currentName)) return;

        var prev = _lastPageName;
        if (prev == currentName) return;

        if (!string.IsNullOrWhiteSpace(prev))
            await BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onDisappear}: {prev}");

        _lastPageName = currentName;
        await BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onAppear}: {currentName}");
    }

    private static string GetDisplayName(Page? p)
    {
        var t = p?.Title;
        if (!string.IsNullOrWhiteSpace(t)) return t.Trim();
        return p?.GetType().Name ?? string.Empty;
    }

    private static Page? GetCurrentPage(Application? app)
    {
        return app?.Windows?.FirstOrDefault()?.Page;
    }
}
