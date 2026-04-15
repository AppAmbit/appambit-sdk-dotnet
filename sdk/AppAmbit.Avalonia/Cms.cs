namespace AppAmbitAvalonia;

public static class Cms
{
    public static AppAmbit.ICmsQueryBuilder<T> Content<T>(string contentType) where T : class
        => AppAmbit.Cms.Content<T>(contentType);

    public static Task ClearCache(string contentType)
        => AppAmbit.Cms.ClearCache(contentType);

    public static Task ClearAllCache()
        => AppAmbit.Cms.ClearAllCache();
}
