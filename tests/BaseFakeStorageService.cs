using AppAmbit.Models.Analytics;
using AppAmbit.Models.Breadcrumbs;
using AppAmbit.Models.Logs;
using AppAmbit.Models.RemoteConfigs;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Interfaces;

namespace AppAmbitTest;

internal abstract class BaseFakeStorageService : IStorageService
{
    public virtual Task InitializeAsync() => Task.CompletedTask;

    // Session Data
    public virtual Task SessionData(SessionData sessionData) => Task.CompletedTask;
    public virtual Task<List<SessionBatch>> GetOldest100SessionsAsync() => Task.FromResult(new List<SessionBatch>());
    public virtual Task DeleteSessionsList(List<SessionBatch> sessions) => Task.CompletedTask;
    public virtual Task<SessionData?> GetUnpairedSessionStart() => Task.FromResult<SessionData?>(null);
    public virtual Task<SessionData?> GetUnpairedSessionEnd() => Task.FromResult<SessionData?>(null);
    public virtual Task DeleteSessionById(string id) => Task.CompletedTask;
    public virtual Task UpdateSessionIdsForAllTrackingData(List<SessionBatch> sessions) => Task.CompletedTask;

    // Device / User / App Info
    public virtual Task SetDeviceId(string? deviceId) => Task.CompletedTask;
    public virtual Task<string?> GetDeviceId() => Task.FromResult<string?>(null);
    public virtual Task SetUserId(string userId) => Task.CompletedTask;
    public virtual Task<string?> GetUserId() => Task.FromResult<string?>(null);
    public virtual Task SetUserEmail(string? email) => Task.CompletedTask;
    public virtual Task<string?> GetUserEmail() => Task.FromResult<string?>(null);
    public virtual Task SetAppId(string? appId) => Task.CompletedTask;
    public virtual Task<string?> GetAppId() => Task.FromResult<string?>(null);
    public virtual Task<string?> GetConsumerId() => Task.FromResult<string?>(null);
    public virtual Task SetConsumerId(string consumerId) => Task.CompletedTask;

    // Push Notifications
    public virtual Task<string?> GetPushDeviceToken() => Task.FromResult<string?>(null);
    public virtual Task SetPushDeviceToken(string? token) => Task.CompletedTask;
    public virtual Task<bool?> GetPushEnabled() => Task.FromResult<bool?>(null);
    public virtual Task SetPushEnabled(bool enabled) => Task.CompletedTask;

    // Logs
    public virtual Task<List<LogEntity>> GetOldest100LogsAsync() => Task.FromResult(new List<LogEntity>());
    public virtual Task LogEventAsync(LogEntity logEntity) => Task.CompletedTask;
    public virtual Task DeleteLogList(List<LogEntity> logs) => Task.CompletedTask;

    // Analytics Events
    public virtual Task LogAnalyticsEventAsync(EventEntity analyticsLog) => Task.CompletedTask;
    public virtual Task<List<EventEntity>> GetOldest100EventsAsync() => Task.FromResult(new List<EventEntity>());
    public virtual Task DeleteEventList(List<EventEntity> logs) => Task.CompletedTask;

    // Breadcrumbs
    public virtual Task<List<BreadcrumbsEntity>> GetOldest100BreadcrumbsAsync() => Task.FromResult(new List<BreadcrumbsEntity>());
    public virtual Task AddBreadcrumbAsync(BreadcrumbsEntity breadcrumb) => Task.CompletedTask;
    public virtual Task DeleteBreadcrumbs(List<BreadcrumbsEntity> breadcrumbs) => Task.CompletedTask;

    // Remote Configs
    public virtual Task AddConfigsAsync(List<RemoteConfigEntity> configs) => Task.CompletedTask;
    public virtual Task<string?> GetConfig(string key) => Task.FromResult<string?>(null);
}
