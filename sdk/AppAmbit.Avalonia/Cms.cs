namespace AppAmbitAvalonia;

public static class Cms
{
    public static AppAmbit.ICmsQueryBuilder<T> Content<T>(string contentType) where T : class
        => AppAmbit.Cms.Content<T>(contentType);
}
