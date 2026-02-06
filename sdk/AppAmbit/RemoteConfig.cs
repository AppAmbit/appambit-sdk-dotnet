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
    private static RemoteConfigResponse? _fetchedConfig;
    private static Dictionary<string, object> _defaults = [];

    public static void Initialize(IStorageService? storageService, IAppInfoService? appInfoService, IAPIService? apiService)
    {
        _storageService = storageService;
        _appInfoService = appInfoService;
        _apiService = apiService;
    }

    public static void SetDefaults(Dictionary<string, object> defaults)
    {
        _defaults = defaults ?? [];
        Debug.WriteLine($"[RemoteConfig] Defaults set. Count: {_defaults.Count}");
        foreach (var key in _defaults.Keys)
        {
            Debug.WriteLine($"[RemoteConfig] Default key: {key}, Value: {_defaults[key]}");
        }
    }

    public static async Task<Boolean> Fetch()
    {
        try
        {
            if (_apiService == null || _appInfoService == null)
            {
                Debug.WriteLine("[RemoteConfig] APIService or AppInfoService is null. Cannot fetch remote config.");
                return false;
            }

            var remoteConfigResponse = await _apiService?.ExecuteRequest<RemoteConfigResponse>(new RemoteConfigEndpoint(_appInfoService.AppVersion));
            if (remoteConfigResponse?.ErrorType == ApiErrorType.NetworkUnavailable)
            {
                Debug.WriteLine("[RemoteConfig] Network unavailable. Cannot fetch remote config.");
                return false;
            }
            _fetchedConfig = remoteConfigResponse?.Data;
            Debug.WriteLine("[RemoteConfig] Successfully fetched remote config.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteConfig] Exception during Fetch: {ex}");
            return false;
        }
    }

    public static async Task<Boolean> Activate()
    {
        try
        {
            if (_fetchedConfig == null || _fetchedConfig.Configs == null)
            {
                Debug.WriteLine("[RemoteConfig] No remote config to activate.");
                return false;
            }

            var configsToSave = _fetchedConfig.Configs.Select(kvp => new RemoteConfigEntity
            {
                Id = Guid.NewGuid(),
                Key = kvp.Key,
                Value = kvp.Value?.ToString()
            }).ToList();

            await _storageService.AddConfigsAsync(configsToSave);

            Debug.WriteLine("[RemoteConfig] Remote config activated.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteConfig] Exception during Activate: {ex}");
            return false;
        }
    }

    public static async Task<Boolean> FetchAndActivate()
    {
        var fetchResult = await Fetch();
        if (fetchResult)
        {
            return await Activate();
        }
        return false;
    }

    public static int GetInt(String key)
    {
        var value = GetValue(key);
        
        if (value is int intValue)
            return intValue;
    
        if (value is long longValue)
            return (int)longValue;
            
        if (value is double doubleValue)
            return (int)doubleValue;
            
        if (int.TryParse(value?.ToString(), out int parsedValue))
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

        if (_defaults.ContainsKey(key))
        {
            Debug.WriteLine($"[RemoteConfig] fetching '{key}' from defaults: {_defaults[key]}");
            return _defaults[key];
        }

        Debug.WriteLine($"[RemoteConfig] Key '{key}' not found in remote config or defaults. Defaults count: {_defaults.Count}");
        return null;
    }catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteConfig] Exception in getValue for key '{key}': {ex}");
            return null;
        }
    }

}