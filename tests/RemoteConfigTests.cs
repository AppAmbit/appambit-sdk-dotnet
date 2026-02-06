using System.Reflection;
using AppAmbit;
using AppAmbit.Models.RemoteConfigs;
using AppAmbit.Services.Interfaces;
using AppAmbit.Services;
using Xunit;

namespace AppAmbitTest;

public class RemoteConfigTests : IDisposable
{
    public RemoteConfigTests() => ResetState();
    public void Dispose() => ResetState();

    [Fact]
    public void SetDefaults_And_GetString_ReturnsDefault_WhenDbEmpty()
    {
        // Arrange
        var storage = new FakeStorageService();
        AppAmbit.RemoteConfig.Initialize(storage, null, null);

        var defaults = new Dictionary<string, object>
        {
            { "banner", false },
            { "data", "Offline Mode" },
            { "discount", 5 }
        };

        // Act
        AppAmbit.RemoteConfig.SetDefaults(defaults);
        
        // Assert
        var data = AppAmbit.RemoteConfig.GetString("data");
        Assert.Equal("Offline Mode", data);

        var banner = AppAmbit.RemoteConfig.GetBoolean("banner");
        Assert.False(banner);

        var discount = AppAmbit.RemoteConfig.GetInt("discount");
        Assert.Equal(5, discount);
    }

    private void ResetState()
    {
        // Access private static fields via reflection to reset RemoteConfig state
        var type = typeof(AppAmbit.RemoteConfig);
        var defaultsField = type.GetField("_defaults", BindingFlags.NonPublic | BindingFlags.Static);
        defaultsField?.SetValue(null, new Dictionary<string, object>());

        var storageField = type.GetField("_storageService", BindingFlags.NonPublic | BindingFlags.Static);
        storageField?.SetValue(null, null);
    }

    private class FakeStorageService : IStorageService
    {
        private Dictionary<string, string> _configs = new();

        public Task AddConfigsAsync(List<RemoteConfigEntity> configs)
        {
            foreach (var c in configs) _configs[c.Key] = c.Value;
            return Task.CompletedTask;
        }

        public Task<string?> GetConfig(string key)
        {
            _configs.TryGetValue(key, out var val);
            return Task.FromResult(val);
        }

        // Other interface members with dummy implementation
        public Task InitializeAsync() => Task.CompletedTask;
        public Task SetDeviceId(string? deviceId) => Task.CompletedTask;
        public Task<string?> GetDeviceId() => Task.FromResult<string?>(null);
        public Task SetAppId(string? appId) => Task.CompletedTask;
        public Task<string?> GetAppId() => Task.FromResult<string?>(null);
        public Task SetUserId(string userId) => Task.CompletedTask;
        public Task<string?> GetUserId() => Task.FromResult<string?>(null);
        public Task SetUserEmail(string? email) => Task.CompletedTask;
        public Task<string?> GetUserEmail() => Task.FromResult<string?>(null);
        public Task<string?> GetConsumerId() => Task.FromResult<string?>(null);
        public Task SetConsumerId(string consumerId) => Task.CompletedTask;
        public Task<string?> GetPushDeviceToken() => Task.FromResult<string?>(null);
        public Task SetPushDeviceToken(string? token) => Task.CompletedTask;
        public Task<bool?> GetPushEnabled() => Task.FromResult<bool?>(null);
        public Task SetPushEnabled(bool enabled) => Task.CompletedTask;
        public Task LogEventAsync(AppAmbit.Models.Logs.LogEntity logEntity) => Task.CompletedTask;
        public Task LogAnalyticsEventAsync(AppAmbit.Models.Analytics.EventEntity analyticsLog) => Task.CompletedTask;
        public Task<List<AppAmbit.Models.Logs.LogEntity>> GetOldest100LogsAsync() => Task.FromResult(new List<AppAmbit.Models.Logs.LogEntity>());
        public Task DeleteLogList(List<AppAmbit.Models.Logs.LogEntity> logs) => Task.CompletedTask;
        public Task DeleteAllLogs() => Task.CompletedTask;
        public Task<List<AppAmbit.Models.Analytics.EventEntity>> GetOldest100EventsAsync() => Task.FromResult(new List<AppAmbit.Models.Analytics.EventEntity>());
        public Task DeleteEventList(List<AppAmbit.Models.Analytics.EventEntity> logs) => Task.CompletedTask;
        public Task SessionData(AppAmbit.Models.Analytics.SessionData sessionData) => Task.CompletedTask;
        public Task<List<AppAmbit.Models.Analytics.SessionBatch>> GetOldest100SessionsAsync() => Task.FromResult(new List<AppAmbit.Models.Analytics.SessionBatch>());
        public Task DeleteSessionsList(List<AppAmbit.Models.Analytics.SessionBatch> sessions) => Task.CompletedTask;
        public Task<AppAmbit.Models.Analytics.SessionData?> GetUnpairedSessionStart() => Task.FromResult<AppAmbit.Models.Analytics.SessionData?>(null);
        public Task<AppAmbit.Models.Analytics.SessionData?> GetUnpairedSessionEnd() => Task.FromResult<AppAmbit.Models.Analytics.SessionData?>(null);
        public Task DeleteSessionById(string id) => Task.CompletedTask;
        public Task UpdateSessionIdsForAllTrackingData(List<AppAmbit.Models.Analytics.SessionBatch> sessions) => Task.CompletedTask;
        public Task<List<AppAmbit.Models.Breadcrumbs.BreadcrumbsEntity>> GetOldest100BreadcrumbsAsync() => Task.FromResult(new List<AppAmbit.Models.Breadcrumbs.BreadcrumbsEntity>());
        public Task AddBreadcrumbAsync(AppAmbit.Models.Breadcrumbs.BreadcrumbsEntity breadcrumb) => Task.CompletedTask;
        public Task DeleteBreadcrumbs(List<AppAmbit.Models.Breadcrumbs.BreadcrumbsEntity> breadcrumbs) => Task.CompletedTask;
    }
}
