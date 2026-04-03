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

    internal static void ResetFetchState(string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            lock (_fetchedContentTypes)
            {
                _fetchedContentTypes.Clear();
            }

            _refreshLocks.Clear();
            return;
        }

        lock (_fetchedContentTypes)
        {
            _fetchedContentTypes.Remove(contentType);
        }

        _refreshLocks.TryRemove(contentType, out _);
    }

    internal static void Initialize(IAPIService? apiService, IStorageService? storageService)
    {
        _apiService = apiService;
        _storageService = storageService;
        ResetFetchState();
    }

    internal static IAPIService? ApiService => _apiService;
    internal static IStorageService? StorageService => _storageService;

    public static async Task ClearCache(string contentType)
    {
        ResetFetchState(contentType);
        if (StorageService != null)
        {
            await StorageService.DeleteCmsEntryAsync(contentType).ConfigureAwait(false);
        }
    }

    public static async Task ClearAllCache()
    {
        ResetFetchState();
        if (StorageService != null)
        {
            await StorageService.DeleteAllCmsEntriesAsync().ConfigureAwait(false);
        }
    }

    public static ICmsQueryBuilder<T> Content<T>(string contentType) where T : class
    {
        return new CmsQueryBuilder<T>(contentType);
    }
}
