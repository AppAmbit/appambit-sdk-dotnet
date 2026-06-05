using AppAmbit.Services.Interfaces;

namespace AppAmbit;

public static class Cms
{
    private static IAPIService? _apiService;

    internal static void Initialize(IAPIService? apiService)
    {
        _apiService = apiService;
    }

    internal static IAPIService? ApiService => _apiService;

    public static ICmsQueryBuilder<T> Content<T>(string contentType) where T : class
    {
        return new CmsQueryBuilder<T>(contentType);
    }
}
