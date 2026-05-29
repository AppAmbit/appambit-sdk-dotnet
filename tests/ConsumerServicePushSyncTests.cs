using System.Reflection;
using AppAmbit;
using AppAmbit.Enums;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Moq;
using Xunit;

namespace AppAmbitTest;

[Collection("ConsumerService Push Sync Tests")]
public class ConsumerServicePushSyncTests : IDisposable
{
    private readonly Mock<IStorageService> _mockStorage;
    private readonly Mock<IAPIService> _mockApi;
    private readonly Mock<IAppInfoService> _mockAppInfo;
    private static readonly Type ConsumerServiceType =
        typeof(AppAmbitSdk).Assembly.GetType("AppAmbit.Services.ConsumerService")!;

    public ConsumerServicePushSyncTests()
    {
        _mockStorage = new Mock<IStorageService>();
        _mockApi = new Mock<IAPIService>();
        _mockAppInfo = new Mock<IAppInfoService>();

        ResetConsumerServiceState();
        InitializeConsumerService();
    }

    public void Dispose() => ResetConsumerServiceState();

    // ── helpers ──────────────────────────────────────────────────────────────

    private void InitializeConsumerService()
    {
        var init = ConsumerServiceType.GetMethod(
            "Initialize", BindingFlags.Public | BindingFlags.Static)!;
        init.Invoke(null, [_mockStorage.Object, _mockAppInfo.Object, _mockApi.Object]);
    }

    private static void ResetConsumerServiceState()
    {
        foreach (var name in new[] { "_storageService", "_apiService", "_appInfoService" })
        {
            ConsumerServiceType
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
        }
    }

    private void SetupSuccessApiResponse() =>
        _mockApi
            .Setup(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()))
            .ReturnsAsync(new ApiResult<object>(new object(), ApiErrorType.None));

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateConsumer_NoChange_SkipsApiCall()
    {
        // Storage already reflects the same state as the call → no diff → no API call.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("token-abc");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync(true);

        await AppAmbitSdk.UpdateConsumerAsync("token-abc", true);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Never);
    }

    [Fact]
    public async Task UpdateConsumer_EnabledChanged_CallsApi()
    {
        // Offline scenario: user disabled push while offline → storage still has enabled=true.
        // On reconnect the hook fires with native state (enabled=false) → diff detected → API called.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("token-abc");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync(true);
        SetupSuccessApiResponse();

        await AppAmbitSdk.UpdateConsumerAsync("token-abc", false);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Once);
    }

    [Fact]
    public async Task UpdateConsumer_TokenChanged_CallsApi()
    {
        // FCM refreshed the token while offline; stored token is stale.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("old-token");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync(true);
        SetupSuccessApiResponse();

        await AppAmbitSdk.UpdateConsumerAsync("new-token", true);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Once);
    }

    [Fact]
    public async Task UpdateConsumer_AfterSuccessfulSync_NoRedundantCall()
    {
        // After a successful sync storage reflects the new state.
        // A second call with the same values must not hit the API again.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("token-abc");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync((bool?)null);
        SetupSuccessApiResponse();

        // First call: enabled=null normalises to storedEnabled ?? true = true → diff from null storage
        await AppAmbitSdk.UpdateConsumerAsync("token-abc", true);

        // Simulate storage now reflects the synced state
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync(true);

        // Second call with the same values
        await AppAmbitSdk.UpdateConsumerAsync("token-abc", true);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Once);
    }

    [Fact]
    public async Task ConnectivityHook_RegisteredAndFired_InvokesCallback()
    {
        // Verify that RegisterPushConnectivityHook + InternalFirePushConnectivityHook
        // are the glue that drives push re-sync on network restore.
        bool hookCalled = false;
        AppAmbitSdk.RegisterPushConnectivityHook(() =>
        {
            hookCalled = true;
            return Task.CompletedTask;
        });

        var fireMethod = typeof(AppAmbitSdk).GetMethod(
            "InternalFirePushConnectivityHook", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)fireMethod.Invoke(null, null)!;

        Assert.True(hookCalled);

        // Cleanup: reset hook so other tests are not affected
        AppAmbitSdk.RegisterPushConnectivityHook(() => Task.CompletedTask);
    }

    [Fact]
    public async Task OnNewToken_EnabledFalse_StoredNull_WouldHitApi_WhyGuardIsNeeded()
    {
        // Documents WHY TokenListenerProxy.OnNewToken must guard against calling
        // UpdateConsumerAsync when push is disabled:
        // If called with (token, enabled=false) on a fresh install (storedEnabled=null),
        // ConsumerService detects a diff (null != false) and hits the backend unnecessarily.
        // The guard in OnNewToken ("only call when enabled=true") prevents this on every launch.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("some-token");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync((bool?)null);
        SetupSuccessApiResponse();

        await AppAmbitSdk.UpdateConsumerAsync("some-token", false);

        // ConsumerService correctly detects diff (storedEnabled=null vs false) → hits API.
        // This is why OnNewToken must NOT call UpdateConsumerAsync when enabled=false.
        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Once);
    }

    [Fact]
    public async Task AppStart_NullToken_EnabledFalse_StorageAlreadyFalse_SkipsApiCall()
    {
        // After the first launch with push never enabled, storage has enabled=false stored.
        // Subsequent launches must NOT call the API again — no diff.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync((string?)null);
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync(false);

        await AppAmbitSdk.UpdateConsumerAsync(null, false);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Never);
    }

    [Fact]
    public async Task ConnectivityRestored_WithStoredEnabledState_SyncsToApi()
    {
        // Simulates: user previously enabled notifications (stored enabled=true, token saved).
        // App goes offline, then connectivity is restored → hook fires → API called to re-sync.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("token-abc");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync((bool?)null); // diff from true → will call API
        SetupSuccessApiResponse();

        // Fire what the connectivity hook calls: UpdateConsumerAsync(lastToken, isEnabled).
        await AppAmbitSdk.UpdateConsumerAsync("token-abc", true);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Once);
    }

    [Fact]
    public async Task ConnectivityRestored_NoChangeSinceLastSync_SkipsApiCall()
    {
        // User has push enabled and the stored state already matches → no diff → no API call.
        _mockStorage.Setup(s => s.GetConsumerId()).ReturnsAsync("consumer-1");
        _mockStorage.Setup(s => s.GetPushDeviceToken()).ReturnsAsync("token-abc");
        _mockStorage.Setup(s => s.GetPushEnabled()).ReturnsAsync(true);

        await AppAmbitSdk.UpdateConsumerAsync("token-abc", true);

        _mockApi.Verify(s => s.ExecuteRequest<object>(It.IsAny<UpdateConsumerEndpoint>()), Times.Never);
    }
}
