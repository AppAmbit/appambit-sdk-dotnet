namespace AppAmbitAvalonia;

public static class RemoteConfig
{
    public static bool Enable()
    {
        return AppAmbit.RemoteConfig.Enable();
    }

    public static Task FetchAndStoreConfig()
    {
        return AppAmbit.RemoteConfig.FetchAndStoreConfig();
    }

    public static long GetLong(string key)
    {
        return AppAmbit.RemoteConfig.GetLong(key);
    }

    public static double GetDouble(string key)
    {
        return AppAmbit.RemoteConfig.GetDouble(key);
    }

    public static bool GetBoolean(string key)
    {
        return AppAmbit.RemoteConfig.GetBoolean(key);
    }

    public static string GetString(string key)
    {
        return AppAmbit.RemoteConfig.GetString(key);
    }
}
