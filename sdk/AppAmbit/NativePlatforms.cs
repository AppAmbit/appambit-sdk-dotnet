using System;
using System.Threading;
using System.Threading.Tasks;
using AppAmbit.Services;

namespace AppAmbit;

internal static partial class NativePlatforms
{
    private static string? _appKey;

    private static readonly SemaphoreSlim _connectivityGate = new(1, 1);

    private static readonly object _bcLock = new();
    private static string? _lastCrumb;
    private static DateTime _lastCrumbAtUtc = DateTime.MinValue;
    private static readonly TimeSpan _crumbWindow = TimeSpan.FromMilliseconds(400);

    public static void Register(string appKey)
    {
        _appKey = appKey;
        PlatformRegister(appKey ?? string.Empty);
        AppAmbitSdk.InternalStart(_appKey ?? string.Empty);
    }

    public static void EnableNativePageBreadcrumbs()
    {
        PlatformEnablePageBreadcrumbs();
    }

    static partial void PlatformRegister(string appKey);
    static partial void PlatformEnablePageBreadcrumbs();

    internal static async Task OnConnectivityAvailableAsync()
    {
        var ok = await NetConnectivity.HasInternetAsync();

        if (!ok || Analytics._isManualSessionEnabled) return;

        if (!await _connectivityGate.WaitAsync(0)) return;
        try
        {
            await AppAmbitSdk.InternalEnsureToken(null);

            BreadcrumbManager.LoadBreadcrumbsFromFile();
            await SessionManager.SendEndSessionFromDatabase();
            await SessionManager.SendStartSessionIfExist();
            await Crashes.LoadCrashFileIfExists();            
            await AppAmbitSdk.InternalSendPending();
        }
        finally
        {
            _connectivityGate.Release();
        }
    }

    internal static Task AddCrumb(string name)
    {
        var now = DateTime.UtcNow;
        lock (_bcLock)
        {
            if (_lastCrumb == name && (now - _lastCrumbAtUtc) < _crumbWindow)
                return Task.CompletedTask; 

            _lastCrumb = name;
            _lastCrumbAtUtc = now;
        }
        return BreadcrumbManager.AddAsync(name);
    }

    internal static void AddCrumbFile(string name)
    {
        var now = DateTime.UtcNow;
        lock (_bcLock)
        {
            if (_lastCrumb == name && (now - _lastCrumbAtUtc) < _crumbWindow)
                return;

            _lastCrumb = name;
            _lastCrumbAtUtc = now;
        }
        
        BreadcrumbManager.SaveFile(name);
    }    
}
