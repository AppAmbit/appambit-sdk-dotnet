using AppAmbit.Models.Analytics;
using AppAmbit.Models.Breadcrumbs;
using AppAmbit.Models.Logs;

namespace AppAmbit.Services.Interfaces;

public interface IStorageService
{
    Task InitializeAsync();

    #region  Sessions
    Task SessionData(SessionData sessionData);

    Task<List<SessionBatch>> GetOldest100SessionsAsync();

    Task DeleteSessionsList(List<SessionBatch> sessions);

    Task<SessionData?> GetUnpairedSessionStart();

    Task<SessionData?> GetUnpairedSessionEnd();

    Task DeleteSessionById(string id);

    Task UpdateSessionIdsForAllTrackingData(List<SessionBatch> sessions);
    
    #endregion

    #region AppSecrets
    Task SetDeviceId(string? deviceId);

    Task<string?> GetDeviceId();

    Task SetUserId(string userId);

    Task<string?> GetUserId();

    Task SetUserEmail(string? email);

    Task<string?> GetUserEmail();

    Task SetAppId(string? appId);

    Task<string?> GetAppId();

    Task<string?> GetConsumerId();

    Task SetConsumerId(string consumerId);

    Task<string?> GetPushDeviceToken();

    Task SetPushDeviceToken(string? token);

    Task<bool?> GetPushEnabled();

    Task SetPushEnabled(bool enabled);
    #endregion

    #region Logs
    Task<List<LogEntity>> GetOldest100LogsAsync();

    Task LogEventAsync(LogEntity logEntity);

    Task DeleteLogList(List<LogEntity> logs);
    #endregion

    #region "Events"
    Task LogAnalyticsEventAsync(EventEntity analyticsLog);

    Task<List<EventEntity>> GetOldest100EventsAsync();

    Task DeleteEventList(List<EventEntity> logs);
    #endregion

    #region Breadcrumbs
    Task<List<BreadcrumbsEntity>> GetOldest100BreadcrumbsAsync();
    Task AddBreadcrumbAsync(BreadcrumbsEntity breadcrumb);
    Task DeleteBreadcrumbs(List<BreadcrumbsEntity> breadcrumbs);
    #endregion
}
