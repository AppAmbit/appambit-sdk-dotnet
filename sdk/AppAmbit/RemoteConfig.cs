using System.Diagnostics;
using AppAmbit.Enums;
using AppAmbit.Models.RemoteConfigs;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;

namespace AppAmbit;

public static class RemoteConfig
{
    private static IAPIService? _apiService;
    private static IStorageService? _storageService;
    private static IAppInfoService? _appInfoService;


    public static void Initialize(IStorageService? storageService, IAppInfoService? appInfoService, IAPIService? apiService)
    {
        _storageService = storageService;
        _appInfoService = appInfoService;
        _apiService = apiService;
    }

    private static bool _isEnable = false;
    private static bool _isFetchCompleted = false;

    public static bool Enable()
    {
        return _isEnable = true;
    }

    public static async Task FetchAndStoreConfig()
    {
        if (!_isEnable || _isFetchCompleted)
            return;

        if (_apiService == null || _appInfoService == null || _storageService == null)
        {
            Debug.WriteLine("[RemoteConfig] No initialized services");
            return;
        }

        try
        {
            var remoteConfigResponse = await _apiService.ExecuteRequest<RemoteConfigResponse>(new RemoteConfigEndpoint(_appInfoService.AppVersion));

            if (remoteConfigResponse?.ErrorType == ApiErrorType.None)
            {
                if (remoteConfigResponse.Data != null && remoteConfigResponse.Data.Configs != null)
                {
                    var configsToSave = remoteConfigResponse.Data.Configs.Select(kvp => new RemoteConfigEntity
                    {
                        Id = Guid.NewGuid(),
                        Key = kvp.Key,
                        Value = kvp.Value?.ToString()
                    }).ToList();

                    await _storageService.AddConfigsAsync(configsToSave);
                }
                _isFetchCompleted = true;
            }
        }
        catch (Exception ex)
        {
           Debug.WriteLine($"[RemoteConfig] Exception: {ex}");
        }
    }

    public static long GetLong(String key)
    {
        var value = GetValue(key);

        if (value is long longValue)
            return longValue;
            
        if (value is double doubleValue)
            return (long)doubleValue;
            
        if (long.TryParse(value?.ToString(), out long parsedValue))
            return parsedValue;
            
        return 0;
    }

    public static double GetDouble(String key)
    {
        var value = GetValue(key);
        
        if (value is double doubleValue)
            return doubleValue;
            
        if (value is int intValue)
            return intValue;
            
        if (value is long longValue)
            return longValue;
            
        if (double.TryParse(value?.ToString(), out double parsedValue))
            return parsedValue;
            
        return 0.0;
    }

    public static bool GetBoolean(String key)
    {
        var value = GetValue(key);
        
        if (value is Boolean boolValue)
            return boolValue;
            
        if (bool.TryParse(value?.ToString(), out bool parsedValue))
            return parsedValue;
            
        return false;
    }

    public static String GetString(String key)
    {
        var value = GetValue(key);
        return value?.ToString() ?? String.Empty;
    }

    private static Object? GetValue(String key)
    {
        try
        {
            if (_storageService != null)
            {
                try
                {
                    var dbValue = AsyncHelpers.RunSync(() => _storageService.GetConfig(key));
                    if (dbValue != null)
                    {
                       Debug.WriteLine($"[RemoteConfig] fetching '{key}' from database: {dbValue}");
                       return dbValue;
                    }
                    Debug.WriteLine($"[RemoteConfig] Key '{key}' not found in database.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RemoteConfig] Error fetching key '{key}' from database: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("[RemoteConfig] StorageService is null. Skipping database lookup.");
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteConfig] Exception in getValue for key '{key}': {ex}");
            return null;
        }
    }

}