using System.Reflection;
using AppAmbit;
using AppAmbit.Enums;
using AppAmbit.Models.Analytics;
using AppAmbit.Models.Breadcrumbs;
using AppAmbit.Models.Logs;
using AppAmbit.Models.Responses;
using AppAmbit.Services;
using AppAmbit.Services.Interfaces;

namespace AppAmbitTest;

/// <summary>
/// Unit test examples for Crashes using fakes (no DB/network).
/// </summary>
public class CrashesExamples : IDisposable
{
    public CrashesExamples() => ResetState();
    public void Dispose() => ResetState();

    // Ensures a plain error message is persisted as an error log with timestamp.
    [Fact]
    public async Task LogError_PersistsErrorLog()
    {
        var (api, storage, appInfo) = BuildFakes();
        InitializeSdk(api, storage, appInfo);

        await EnsureSessionAsync(storage);
        await Crashes.LogError("boom!");
        await Task.Delay(20);

        var log = Assert.Single(storage.Logs);
        Assert.Equal(LogType.Error, log.Type);
        Assert.Equal("boom!", log.Message);
        Assert.True(log.CreatedAt != default);
    }

    // Ensures an exception-based log captures the exception message in storage.
    [Fact]
    public async Task LogError_FromException_PersistsCrashFilePath()
    {
        var (api, storage, appInfo) = BuildFakes();
        InitializeSdk(api, storage, appInfo);

        await EnsureSessionAsync(storage);
        await Crashes.LogError(new InvalidOperationException("bad"), classFqn: "Test.Class");
        await Task.Delay(20);

        var log = Assert.Single(storage.Logs);
        Assert.Equal(LogType.Error, log.Type);
        Assert.Contains("bad", log.Message);
    }

    // When API succeeds, pending logs are sent and removed from local storage.
    [Fact]
    public async Task SendBatchLogs_WithApiSuccess_RemovesLogsFromStorage()
    {
        var (api, storage, appInfo) = BuildFakes(ApiErrorType.None);
        InitializeSdk(api, storage, appInfo);

        storage.Logs.Add(new LogEntity { Id = Guid.NewGuid(), Message = "old-1", CreatedAt = DateTime.UtcNow.AddDays(-1) });
        storage.Logs.Add(new LogEntity { Id = Guid.NewGuid(), Message = "old-2", CreatedAt = DateTime.UtcNow });

        await Crashes.SendBatchLogs();

        Assert.Empty(storage.Logs);
        Assert.Equal(1, api.RequestCount);
    }

    // Backdated logs are still batched, sent, and cleared when API is available.
    [Fact]
    public async Task SendBatchLogs_WithBackdatedLogs_StillSendsBatch()
    {
        var (api, storage, appInfo) = BuildFakes(ApiErrorType.None);
        InitializeSdk(api, storage, appInfo);

        var past = DateTime.UtcNow.AddDays(-3);
        storage.Logs.Add(new LogEntity { Id = Guid.NewGuid(), Message = "past-1", CreatedAt = past });
        storage.Logs.Add(new LogEntity { Id = Guid.NewGuid(), Message = "past-2", CreatedAt = past.AddMinutes(5) });

        await Crashes.SendBatchLogs();

        Assert.Empty(storage.Logs);
        Assert.Equal(1, api.RequestCount);
    }

    private static (FakeApiService api, FakeStorageService storage, FakeAppInfoService appInfo) BuildFakes(ApiErrorType apiError = ApiErrorType.NetworkUnavailable)
    {
        return (new FakeApiService(apiError), new FakeStorageService(), new FakeAppInfoService());
    }

    private static void InitializeSdk(FakeApiService api, FakeStorageService storage, FakeAppInfoService appInfo)
    {
        SessionManager.Initialize(api, storage);
        BreadcrumbManager.Initialize(api, storage);
        Analytics.Initialize(api, storage);
        TokenService.Initialize(storage);
        ConsumerService.Initialize(storage, appInfo, api);
        Crashes.Initialize(api, storage, "device-id");
    }

    private static void ResetState()
    {
        SetStaticField(typeof(AppAmbitSdk), "apiService", null);
        SetStaticField(typeof(AppAmbitSdk), "storageService", null);
        SetStaticField(typeof(AppAmbitSdk), "appInfoService", null);
        SetStaticField(typeof(AppAmbitSdk), "_servicesReady", false);
        SetStaticField(typeof(AppAmbitSdk), "_hasStartedSession", false);
        SetStaticField(typeof(AppAmbitSdk), "_skippedFirstResume", false);
        SetStaticField(typeof(AppAmbitSdk), "_configuredByBuilder", false);

        var sessionType = typeof(SessionManager);
        SetStaticField(sessionType, "_apiService", null);
        SetStaticField(sessionType, "_storageService", null);
        SetStaticField(sessionType, "_sessionId", null);
        SetStaticField(sessionType, "_isSessionActive", false);

        var bcType = typeof(BreadcrumbManager);
        SetStaticField(bcType, "_api", null);
        SetStaticField(bcType, "_storage", null);
        SetStaticField(bcType, "_lastBreadcrumb", null);
        SetStaticField(bcType, "_lastBreadcrumbAtMs", 0L);

        var analyticsType = typeof(Analytics);
        SetStaticField(analyticsType, "_isManualSessionEnabled", false);
        SetStaticField(analyticsType, "_apiService", null);
        SetStaticField(analyticsType, "_storageService", null);

        var crashesType = typeof(Crashes);
        SetStaticField(crashesType, "_apiService", null);
        SetStaticField(crashesType, "_storageService", null);
        SetStaticField(crashesType, "_deviceId", "");
        SetStaticField(crashesType, "_crashFlagEvaluated", false);

        var loggingType = typeof(Logging);
        SetStaticField(loggingType, "_apiService", null);
        SetStaticField(loggingType, "_storageService", null);
        SetStaticField(loggingType, "_deviceId", null);
    }

    private static async Task EnsureSessionAsync(FakeStorageService storage)
    {
        if (!SessionManager.IsSessionActive)
        {
            await SessionManager.StartSession();
            if (!SessionManager.IsSessionActive)
            {
                SetStaticField(typeof(SessionManager), "_isSessionActive", true);
                SetStaticField(typeof(SessionManager), "_sessionId", Guid.NewGuid().ToString());
                await storage.SessionData(new SessionData
                {
                    Id = Guid.NewGuid().ToString(),
                    SessionId = SessionManager.SessionId,
                    SessionType = SessionType.Start,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    private static void SetStaticField(Type t, string name, object? value)
    {
        var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        f?.SetValue(null, value);
    }

    #region Fakes
    private sealed class FakeApiService : IAPIService
    {
        private readonly ApiErrorType _error;
        private string? _token;

        public FakeApiService(ApiErrorType error)
        {
            _error = error;
        }

        public int RequestCount { get; private set; }

        public Task<ApiResult<T>?> ExecuteRequest<T>(IEndpoint endpoint) where T : notnull
        {
            RequestCount++;
            return Task.FromResult<ApiResult<T>?>(new ApiResult<T>(default, _error));
        }

        public void SetToken(string? token) => _token = token;

        public string? GetToken() => _token;

        public Task<ApiErrorType> GetNewToken()
        {
            _token ??= Guid.NewGuid().ToString();
            return Task.FromResult(ApiErrorType.None);
        }
    }

    private sealed class FakeStorageService : IStorageService
    {
        public List<SessionData> Sessions { get; } = new();
        public List<BreadcrumbsEntity> Breadcrumbs { get; } = new();
        public List<LogEntity> Logs { get; } = new();
        public List<EventEntity> Events { get; } = new();

        public Task InitializeAsync() => Task.CompletedTask;

        public Task SessionData(SessionData sessionData)
        {
            Sessions.Add(sessionData);
            return Task.CompletedTask;
        }

        public Task<List<SessionBatch>> GetOldest100SessionsAsync() => Task.FromResult(new List<SessionBatch>());

        public Task DeleteSessionsList(List<SessionBatch> sessions)
        {
            Sessions.RemoveAll(s => sessions.Any(x => x.Id == s.Id));
            return Task.CompletedTask;
        }

        public Task<SessionData?> GetUnpairedSessionStart() => Task.FromResult<SessionData?>(Sessions.FirstOrDefault(s => s.SessionType == SessionType.Start));

        public Task<SessionData?> GetUnpairedSessionEnd() => Task.FromResult<SessionData?>(Sessions.FirstOrDefault(s => s.SessionType == SessionType.End));

        public Task DeleteSessionById(string id)
        {
            Sessions.RemoveAll(s => s.Id == id);
            return Task.CompletedTask;
        }

        public Task UpdateSessionIdsForAllTrackingData(List<SessionBatch> sessions)
        {
            foreach (var sb in sessions)
            {
                foreach (var ev in Events.Where(e => e.SessionId == null))
                    ev.SessionId = sb.SessionId;
                foreach (var bc in Breadcrumbs.Where(b => string.IsNullOrWhiteSpace(b.SessionId)))
                    bc.SessionId = sb.SessionId;
            }
            return Task.CompletedTask;
        }

        public Task SetDeviceId(string? deviceId) => Task.CompletedTask;
        public Task<string?> GetDeviceId() => Task.FromResult<string?>(null);
        public Task SetUserId(string userId) => Task.CompletedTask;
        public Task<string?> GetUserId() => Task.FromResult<string?>(null);
        public Task SetUserEmail(string? email) => Task.CompletedTask;
        public Task<string?> GetUserEmail() => Task.FromResult<string?>(null);
        public Task SetAppId(string? appId) => Task.CompletedTask;
        public Task<string?> GetAppId() => Task.FromResult<string?>(null);
        public Task<string?> GetConsumerId() => Task.FromResult<string?>(null);
        public Task SetConsumerId(string consumerId) => Task.CompletedTask;

        public Task<List<LogEntity>> GetOldest100LogsAsync() => Task.FromResult(Logs.ToList());
        public Task LogEventAsync(LogEntity logEntity)
        {
            Logs.Add(logEntity);
            return Task.CompletedTask;
        }
        public Task DeleteLogList(List<LogEntity> logs)
        {
            foreach (var log in logs)
            {
                Logs.RemoveAll(x => x.Id == log.Id);
            }
            return Task.CompletedTask;
        }

        public Task LogAnalyticsEventAsync(EventEntity analyticsLog)
        {
            Events.Add(analyticsLog);
            return Task.CompletedTask;
        }
        public Task<List<EventEntity>> GetOldest100EventsAsync() => Task.FromResult(Events.ToList());
        public Task DeleteEventList(List<EventEntity> logs) => Task.CompletedTask;

        public Task<List<BreadcrumbsEntity>> GetOldest100BreadcrumbsAsync() => Task.FromResult(Breadcrumbs.ToList());
        public Task AddBreadcrumbAsync(BreadcrumbsEntity breadcrumb)
        {
            Breadcrumbs.Add(breadcrumb);
            return Task.CompletedTask;
        }
        public Task DeleteBreadcrumbs(List<BreadcrumbsEntity> breadcrumbs) => Task.CompletedTask;
    }

    private sealed class FakeAppInfoService : IAppInfoService
    {
        public string? AppVersion { get; set; }
        public string? Build { get; set; }
        public string? Platform { get; set; }
        public string? OS { get; set; }
        public string? DeviceModel { get; set; }
        public string? Country { get; set; }
        public string? UtcOffset { get; set; }
        public string? Language { get; set; }
    }
    #endregion
}
