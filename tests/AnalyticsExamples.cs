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

public class AnalyticsExamples : IDisposable
{
    public AnalyticsExamples() => ResetState();
    public void Dispose() => ResetState();

    [Fact]
    public async Task StartSession_PersistsStartWithDefaultFlow()
    {
        var (api, storage, appInfo) = BuildFakes();
        InitializeSdk(api, storage, appInfo);

        await Analytics.StartSession();

        var session = Assert.Single(storage.Sessions);
        Assert.Equal(SessionType.Start, session.SessionType);
        Assert.True(session.Timestamp != default);
    }

    [Fact]
    public async Task TrackEvent_WhenApiFails_SavesEventLocally()
    {
        var (api, storage, appInfo) = BuildFakes(apiError: ApiErrorType.NetworkUnavailable);
        InitializeSdk(api, storage, appInfo);

        await SessionManager.StartSession();
        await Analytics.TrackEvent("video_started", new Dictionary<string, string> { { "q", "hd" } });

        var evt = Assert.Single(storage.Events);
        Assert.Equal("video_started", evt.Name);
        Assert.NotNull(evt.SessionId);
    }

    [Fact]
    public async Task TrackEvent_WhenApiOk_DoesNotPersistLocally()
    {
        var (api, storage, appInfo) = BuildFakes(apiError: ApiErrorType.None);
        InitializeSdk(api, storage, appInfo);

        await Analytics.TrackEvent("video_started");

        Assert.Empty(storage.Events);
    }

    [Fact]
    public async Task ManualMode_InternalStart_DoesNotAutoStartSession()
    {
        var (api, storage, appInfo) = BuildFakes();
        InitializeSdk(api, storage, appInfo);
        Analytics.EnableManualSession();

        AppAmbitSdk.InternalStart("app-key");
        await Task.Delay(20);

        Assert.Empty(storage.Sessions);
    }

    [Fact]
    public async Task ManualMode_StartAndEndSessionExplicit()
    {
        var (api, storage, appInfo) = BuildFakes();
        InitializeSdk(api, storage, appInfo);
        Analytics.EnableManualSession();

        await EnsureSessionAsync(storage);
        await SessionManager.StartSession();
        await SessionManager.EndSession();

        Assert.Equal(2, storage.Sessions.Count);
        Assert.Equal(SessionType.Start, storage.Sessions[0].SessionType);
        Assert.Equal(SessionType.End, storage.Sessions[1].SessionType);
    }

    [Fact]
    public async Task SendBatchSessions_ResolvesSessionIdsAndUpdatesTracking()
    {
        var (api, storage, appInfo) = BuildFakes();
        InitializeSdk(api, storage, appInfo);

        var baseTime = DateTime.UtcNow;
        var localBatches = Enumerable.Range(0, 10).Select(i => new SessionBatch
        {
            Id = $"local-{i}",
            SessionId = null,
            StartedAt = baseTime.AddHours(i),
            EndedAt = baseTime.AddHours(i).AddMinutes(30)
        }).ToList();

        foreach (var sb in localBatches)
        {
            for (int j = 0; j < 25; j++)
            {
                storage.Events.Add(new EventEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"evt-{sb.Id}-{j}",
                    SessionId = string.Empty,
                    CreatedAt = baseTime
                });

                storage.Breadcrumbs.Add(new BreadcrumbsEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"bc-{sb.Id}-{j}",
                    SessionId = string.Empty,
                    CreatedAt = baseTime
                });
            }
        }

        var serverBatches = localBatches.Select((sb, idx) => new SessionBatch
        {
            Id = sb.Id,
            SessionId = $"srv-{idx}",
            StartedAt = sb.StartedAt,
            EndedAt = sb.EndedAt
        }).ToList();

        var resolved = SessionManager.ResolveSessions(localBatches, serverBatches);

        Assert.Equal(10, resolved.Count);
        Assert.All(resolved, r => Assert.False(string.IsNullOrWhiteSpace(r!.SessionId)));

        await storage.UpdateSessionIdsForAllTrackingData(resolved!);

        Assert.Equal(250, storage.Events.Count);
        Assert.All(storage.Events, e => Assert.Contains("srv-", e.SessionId));
        Assert.Equal(250, storage.Breadcrumbs.Count);
        Assert.All(storage.Breadcrumbs, b => Assert.Contains("srv-", b.SessionId));
    }

    [Fact]
    public async Task SendBatchEvents_WithOldEvents_RemovesEventsFromStorage()
    {
        var (api, storage, appInfo) = BuildFakes(apiError: ApiErrorType.None);
        InitializeSdk(api, storage, appInfo);

        storage.Events.Add(new EventEntity
        {
            Id = Guid.NewGuid(),
            Name = "old_event",
            SessionId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        await Analytics.SendBatchEvents();

        Assert.Empty(storage.Events);
        Assert.True(api.RequestCount > 0);
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
        if (f != null)
        {
            f.SetValue(null, value);
            return;
        }

        var p = t.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Static);
        p?.SetValue(null, value);
    }

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
        public Task DeleteSessionsList(List<SessionBatch> sessions) => Task.CompletedTask;
        public Task<SessionData?> GetUnpairedSessionStart() => Task.FromResult<SessionData?>(null);
        public Task<SessionData?> GetUnpairedSessionEnd() => Task.FromResult<SessionData?>(null);
        public Task DeleteSessionById(string id) => Task.CompletedTask;
        public Task UpdateSessionIdsForAllTrackingData(List<SessionBatch> sessions)
        {
            foreach (var session in sessions)
            {
                if (string.IsNullOrWhiteSpace(session.SessionId) || string.IsNullOrWhiteSpace(session.Id))
                {
                    continue;
                }

                var matchId = session.Id;
                var sessionId = session.SessionId;

                foreach (var evt in Events.Where(e =>
                             string.IsNullOrWhiteSpace(e.SessionId) &&
                             e.Name.Contains(matchId, StringComparison.OrdinalIgnoreCase)))
                {
                    evt.SessionId = sessionId;
                }

                foreach (var bc in Breadcrumbs.Where(b =>
                             string.IsNullOrWhiteSpace(b.SessionId) &&
                             b.Name.Contains(matchId, StringComparison.OrdinalIgnoreCase)))
                {
                    bc.SessionId = sessionId;
                }
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
        public Task DeleteLogList(List<LogEntity> logs) => Task.CompletedTask;

        public Task LogAnalyticsEventAsync(EventEntity analyticsLog)
        {
            Events.Add(analyticsLog);
            return Task.CompletedTask;
        }

        public Task<List<EventEntity>> GetOldest100EventsAsync() => Task.FromResult(Events.ToList());

        public Task DeleteEventList(List<EventEntity> logs)
        {
            foreach (var e in logs)
            {
                Events.RemoveAll(x => x.Id == e.Id);
            }
            return Task.CompletedTask;
        }

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
}
