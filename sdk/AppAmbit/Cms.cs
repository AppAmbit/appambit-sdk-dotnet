using AppAmbit.Services.Interfaces;
using System.Collections.Concurrent;
using System.Threading;

namespace AppAmbit;

public static class Cms
{
    private static IAPIService? _apiService;
    private static IStorageService? _storageService;

    private static readonly HashSet<string> _fetchedContentTypes = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();

    internal static HashSet<string> FetchedContentTypes => _fetchedContentTypes;

    internal static SemaphoreSlim GetRefreshLock(string contentType)
        => _refreshLocks.GetOrAdd(contentType, _ => new SemaphoreSlim(1, 1));

    internal static void Initialize(IAPIService? apiService, IStorageService? storageService)
    {
        _apiService = apiService;
        _storageService = storageService;
    }

    internal static IAPIService? ApiService => _apiService;
    internal static IStorageService? StorageService => _storageService;

    public static Task Clear(string contentType)
        => StorageService?.DeleteCmsEntryAsync(contentType) ?? Task.CompletedTask;

    public static Task ClearAll()
        => StorageService?.DeleteAllCmsEntriesAsync() ?? Task.CompletedTask;

    public static CmsQueryBuilder<T> For<T>(string contentType) where T : class
    {
        return new CmsQueryBuilder<T>(contentType);
    }
}
