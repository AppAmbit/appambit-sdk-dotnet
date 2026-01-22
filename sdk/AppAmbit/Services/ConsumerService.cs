using AppAmbit;
using AppAmbit.Models.App;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using AppAmbit.Enums;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace AppAmbit.Services;

internal class ConsumerService
{
    private static IStorageService? _storageService;
    private static IAPIService? _apiService;

    private static IAppInfoService? _appInfoService;
    private static readonly SemaphoreSlim PushUpdateLock = new(1, 1);

    public static void Initialize(IStorageService? storageService, IAppInfoService? appInfoService, IAPIService? apiService)
    {
        _storageService = storageService;
        _appInfoService = appInfoService;
        _apiService = apiService;
    }

    public static async Task<RegisterEndpoint> BuildRegisterEndpoint()
    {
        string? appId = null;
        string? deviceId = "", userId = "", userEmail = null;
        try
        {
            if (_storageService == null || _appInfoService == null || _apiService == null)
            {
                return new RegisterEndpoint(new Consumer());
            }

            deviceId = await _storageService.GetDeviceId();
            userId = await _storageService.GetUserId();
            userEmail = await _storageService.GetUserEmail();
            appId = await _storageService.GetAppId();

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                await _storageService.SetDeviceId(deviceId);
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = Guid.NewGuid().ToString();
                await _storageService.SetUserId(userId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConsumerService] Error getting data for AppSecrets ConsumerService: {ex}");
        }

        return new RegisterEndpoint(new Consumer
        {
            AppKey = appId ?? "",
            DeviceId = deviceId ?? "",
            DeviceModel = _appInfoService?.DeviceModel ?? "",
            UserId = userId ?? "",
            UserEmail = userEmail,
            OS = _appInfoService?.OS ?? "",
            Country = _appInfoService?.Country ?? "",
            Language = _appInfoService?.Language ?? "",
            AppVersion = _appInfoService.AppVersion
        });
    }

    public static async Task<ApiErrorType> CreateConsumer()
    {
        try
        {
            if (_apiService == null || _storageService == null)
            {
                return ApiErrorType.Unknown;
            }

            var registerEndpoint = await BuildRegisterEndpoint();
            var tokenResponse = await _apiService.ExecuteRequest<TokenResponse>(registerEndpoint);

            if (tokenResponse == null)
            {
                Debug.Print($"[ConsumerService] Token Response is null");
            }

            if (tokenResponse?.ErrorType != ApiErrorType.None)
            {
                Debug.Print($"[ConsumerService] Request failed: {tokenResponse?.ErrorType}");
                return tokenResponse?.ErrorType ?? ApiErrorType.Unknown;
            }

            if (tokenResponse.Data == null)
            {
                Debug.Print("[ConsumerService] TokenResponse.Data is null");
                return ApiErrorType.Unknown;
            }

            await _storageService!.SetConsumerId(tokenResponse.Data.ConsumerId);
            _apiService.SetToken(tokenResponse.Data.Token);

            return tokenResponse.ErrorType;
        }
        catch (Exception ex)
        {
            Debug.Print($"[ConsumerService] {ex.Message}");
            return ApiErrorType.Unknown;
        }
    }

    public static async Task UpdateAppKeyIfNeeded(string appKey)
    {
        if (_storageService == null)
        {
            return;
        }

        string newKey = (appKey ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(newKey))
        {
            return;
        }

        string? storedKey = await _storageService.GetAppId();
        if (string.Equals(storedKey, newKey, StringComparison.Ordinal))
        {
            return;
        }

        await _storageService.SetConsumerId(string.Empty);
        await _storageService.SetAppId(newKey);
    }

    public static async Task UpdateConsumer(string? deviceToken, bool? pushEnabled)
    {
        if (_storageService == null || _apiService == null)
        {
            Debug.WriteLine("[ConsumerService] Cannot update consumer: services not initialized.");
            return;
        }

        await PushUpdateLock.WaitAsync();
        try
        {
            var storedToken = await _storageService.GetPushDeviceToken() ?? string.Empty;
            var storedEnabled = await _storageService.GetPushEnabled();

            var normalizedToken = string.IsNullOrWhiteSpace(deviceToken) ? storedToken : deviceToken.Trim();
            var normalizedEnabled = pushEnabled ?? storedEnabled ?? true;

            if (string.IsNullOrWhiteSpace(normalizedToken) && pushEnabled == null && storedEnabled == null)
            {
                Debug.WriteLine("[ConsumerService] No push data to update (token and flag missing).");
                return;
            }

            await _storageService.SetPushDeviceToken(normalizedToken);
            await _storageService.SetPushEnabled(normalizedEnabled);

            if (!await NetConnectivity.HasInternetAsync())
            {
                Debug.WriteLine("[ConsumerService] No internet connection, backend update deferred.");
                return;
            }

            var consumerId = await _storageService.GetConsumerId();
            if (string.IsNullOrWhiteSpace(consumerId))
            {
                Debug.WriteLine("[ConsumerService] consumerId missing, backend update skipped.");
                return;
            }

            var payloadToken = string.IsNullOrWhiteSpace(normalizedToken) ? null : normalizedToken;
            var payloadEnabled = pushEnabled ?? storedEnabled;

            if (payloadToken == null && payloadEnabled == null)
            {
                Debug.WriteLine("[ConsumerService] Nothing to send to backend (token empty and enabled flag null).");
                return;
            }

            var endpoint = new UpdateConsumerEndpoint(consumerId, new UpdateConsumer(payloadToken, payloadEnabled));
            var response = await _apiService.ExecuteRequest<object>(endpoint);

            if (response != null && response.ErrorType == ApiErrorType.None)
            {
                Debug.WriteLine("[ConsumerService] Consumer push state updated successfully.");
            }
            else if (response != null && response.ErrorType == ApiErrorType.NetworkUnavailable)
            {
                Debug.WriteLine("[ConsumerService] Network unavailable while updating consumer push state.");
            }
            else
            {
                Debug.WriteLine("[ConsumerService] Failed to update consumer push state.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConsumerService] Exception while updating consumer push state: {ex}");
        }
        finally
        {
            PushUpdateLock.Release();
        }
    }

}
