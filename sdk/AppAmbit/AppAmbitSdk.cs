using System.Diagnostics;
using AppAmbit.Services;
using AppAmbit.Services.Interfaces;

namespace AppAmbit;

public static class AppAmbitSdk
{
    private static IAPIService? apiService;
    private static IStorageService? storageService;
    private static IAppInfoService? appInfoService;
    private static bool _hasStartedSession = false;
    private static readonly SemaphoreSlim consumerSemaphore = new(1, 1);
    private static readonly SemaphoreSlim _ensureBatchLocked = new(1, 1);
    private static bool _configuredByBuilder = false;
    private static bool _servicesReady = false;
    private static bool _skippedFirstResume = false;
    public static void MarkConfiguredByBuilder() => _configuredByBuilder = true;

    private static void OnStart(string appKey)
    {
        try
        {
            if (_hasStartedSession) return;

            InitializeServices();
            InitializeConsumer(appKey);

            AsyncHelpers.RunSync(() => BreadcrumbManager.AddAsync(BreadcrumbsConstants.onStart));

            _hasStartedSession = true;
            BreadcrumbManager.LoadBreadcrumbsFromFile();
            AsyncHelpers.RunSync(() => Crashes.LoadCrashFileIfExists());
            AsyncHelpers.RunSync(SendDataPending);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbitSdk] Exception during OnStart initialization: {ex}");
        }
    }

    private static async Task OnResume()
    {

        if (!_servicesReady)
        {
            InitializeServices();
            if (!_servicesReady) return;
        }

        if (!TokenIsValid())
        {
            await GetNewToken(null);
        }

        if (!Analytics._isManualSessionEnabled && _hasStartedSession)
        {
            await SessionManager.RemoveSavedEndSession();
        }

        BreadcrumbManager.LoadBreadcrumbsFromFile();

        if (_skippedFirstResume)
        {
            await BreadcrumbManager.AddAsync(BreadcrumbsConstants.onResume);
        }
        else
        {
            _skippedFirstResume = true;
        }

        await SessionManager.SendEndSessionFromDatabase();
        await SessionManager.SendStartSessionIfExist();
        await Crashes.LoadCrashFileIfExists();
        await SendDataPending();       
    }

    private static void OnSleep()
    {
        if (!Analytics._isManualSessionEnabled)
        {
            BreadcrumbManager.SaveFile(BreadcrumbsConstants.onPause);
            SessionManager.SaveEndSession();
        }
    }

    private static void OnEnd()
    {
        if (!Analytics._isManualSessionEnabled)
        {
            SessionManager.SaveEndSession();
        }
    }

    private static void InitializeConsumer(string appKey)
    {
        if (!Analytics._isManualSessionEnabled)
        {
            AsyncHelpers.RunSync(SessionManager.SaveSessionEndToDatabaseIfExist);
        }

        AsyncHelpers.RunSync(() => GetNewToken(appKey));

        if (Analytics._isManualSessionEnabled)
            return;

        AsyncHelpers.RunSync(SessionManager.SendEndSessionFromDatabase);
        AsyncHelpers.RunSync(SessionManager.SendEndSessionFromFile);
        AsyncHelpers.RunSync(SessionManager.StartSession);
    }

    private static async Task SendDataPending()
    {
        await _ensureBatchLocked.WaitAsync();
        try
        {
            await SessionManager.SendBatchSessions();
            await Crashes.SendBatchLogs();
            await Analytics.SendBatchEvents();
            await BreadcrumbManager.SendBatchBreadcrumbs();
        }
        finally
        {
            _ensureBatchLocked.Release();
        }
    }

    private static void InitializeServices()
    {
        if (_servicesReady) return;

        try
        {
            apiService ??= new APIService();
            appInfoService ??= new AppInfoService();
            storageService ??= new StorageService();

            AsyncHelpers.RunSync(() => storageService!.InitializeAsync());
            ConsumerService.Initialize(storageService, appInfoService, apiService);
            TokenService.Initialize(storageService);

            var deviceId = AsyncHelpers.RunSync(() => storageService.GetDeviceId());

            SessionManager.Initialize(apiService, storageService);
            Crashes.Initialize(apiService, storageService, deviceId ?? "");
            Analytics.Initialize(apiService, storageService);
            ConsumerService.Initialize(storageService, appInfoService, apiService);

            BreadcrumbManager.Initialize(apiService!, storageService!);

            _servicesReady = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            _servicesReady = false;
            throw;
        }
    }

    private static async Task GetNewToken(string? appKey, string? pushToken = null, bool? pushEnabled = null)
    {
        await consumerSemaphore.WaitAsync();
        try
        {
            if (!_servicesReady) return;
            if (storageService == null) return;

            await ConsumerService.UpdateAppKeyIfNeeded(appKey ?? string.Empty);

            if (!TokenIsValid())
            {
                var consumerId = await storageService.GetConsumerId();
                if (!string.IsNullOrWhiteSpace(consumerId))
                    _ = await apiService!.GetNewToken();
                else
                    _ = await ConsumerService.CreateConsumer();
            }

            await ConsumerService.UpdateConsumer(pushToken, pushEnabled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbitSdk] Exception during token operation: {ex}");
        }
        finally
        {
            consumerSemaphore.Release();
        }
    }

    private static bool TokenIsValid()
    {
        var token = apiService?.GetToken();
        return !string.IsNullOrEmpty(token);
    }

    public static void Start(string appKey)
    {
#if ANDROID
            HookPlatformLifecycle(appKey);
#elif IOS && !MACCATALYST
            HookPlatformLifecycle(appKey);
#elif MACCATALYST
            AppAmbitMacOs.Register(appKey);
#elif WINDOWS
            AppAmbitWindows.Register(appKey);
#else
            if (OperatingSystem.IsWindows())
            {
                AppAmbitWindows.Register(appKey);
            }
            else
            {
                StartFromHostLifecycle(appKey);
            }
#endif
    }

    /// <summary>
    /// Starts the SDK when the host (e.g., MAUI/Avalonia) already handles lifecycle callbacks.
    /// This avoids registering additional native lifecycle hooks.
    /// </summary>
    public static void StartFromHostLifecycle(string appKey) => OnStart(appKey);

    public static Task ResumeAsync() => OnResume();

    public static void Sleep() => OnSleep();

    public static void End() => OnEnd();

    public static Task EnsureTokenAsync(string? appKey = null, string? pushToken = null, bool? pushEnabled = null) => GetNewToken(appKey, pushToken, pushEnabled);

    public static Task UpdateConsumerAsync(string? pushToken, bool? pushEnabled) => ConsumerService.UpdateConsumer(pushToken, pushEnabled);

    public static Task SendPendingAsync() => SendDataPending();

    public static bool IsTokenValid() => TokenIsValid();

    private static void HookPlatformLifecycle(string appKey)
    {
        try
        {
            NativePlatforms.Register(appKey);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error to start: {ex.Message}");             
        }
    }

    internal static void InternalStart(string appKey) => OnStart(appKey);
    internal static Task InternalResume() => OnResume();
    internal static void InternalSleep() => OnSleep();
    internal static void InternalEnd() => OnEnd();
    internal static Task InternalEnsureToken(string? appKey) => GetNewToken(appKey);
    internal static Task InternalSendPending() => SendDataPending();
    internal static bool InternalTokenIsValid() => TokenIsValid();
}
