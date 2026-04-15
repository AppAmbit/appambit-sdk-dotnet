using AppAmbit.Models.Cms;
using AppAmbit.Services;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;

namespace AppAmbit;

public class CmsQueryBuilder<T> : ICmsQueryBuilder<T> where T : class
{
    private readonly string _contentType;
    private static IAPIService? _apiService => Cms.ApiService;
    private static IStorageService? _storageService => Cms.StorageService;

    private readonly StringBuilder _sqlClause = new();
    private readonly List<string> _selectionArgs = new();
    private string? _orderByClause;
    private bool _isPaginated;
    private int _page = 1;
    private int _perPage = 20;

    private int PaginationLimit  => _isPaginated ? _perPage : 0;
    private int PaginationOffset => _isPaginated ? (_page - 1) * _perPage : 0;

    private static readonly JsonSerializerSettings _deserializeSettings = new()
    {
        Error = (_, args) => args.ErrorContext.Handled = true,
        NullValueHandling = NullValueHandling.Ignore
    };

    internal CmsQueryBuilder(string contentType)
    {
        _contentType = contentType;
    }


    public ICmsQueryBuilder<T> Search(string query)
    {
        var trimmed = query?.Trim() ?? "";
        if (!string.IsNullOrEmpty(trimmed))
            AppendClause("e.value LIKE ?", $"%{trimmed}%");
        return this;
    }

    public ICmsQueryBuilder<T> Equals(string field, string value)          => AddCondition(field, "=",    value);
    public ICmsQueryBuilder<T> NotEquals(string field, string value)       => AddCondition(field, "!=",   value);
    public ICmsQueryBuilder<T> Contains(string field, string value)        => AddCondition(field, "LIKE", $"%{value}%");
    public ICmsQueryBuilder<T> StartsWith(string field, string value)      => AddCondition(field, "LIKE", $"{value}%");

    public ICmsQueryBuilder<T> GreaterThan(string field, object value)        => AddNumericCondition(field, ">",  value);
    public ICmsQueryBuilder<T> GreaterThanOrEqual(string field, object value) => AddNumericCondition(field, ">=", value);
    public ICmsQueryBuilder<T> LessThan(string field, object value)           => AddNumericCondition(field, "<",  value);
    public ICmsQueryBuilder<T> LessThanOrEqual(string field, object value)    => AddNumericCondition(field, "<=", value);

    public ICmsQueryBuilder<T> InList(string field, IEnumerable<string> values)    => AddListCondition(field, exists: true,  values);
    public ICmsQueryBuilder<T> NotInList(string field, IEnumerable<string> values) => AddListCondition(field, exists: false, values);

    public ICmsQueryBuilder<T> OrderByAscending(string field)  => SetOrderBy(field, "ASC");
    public ICmsQueryBuilder<T> OrderByDescending(string field) => SetOrderBy(field, "DESC");

    public ICmsQueryBuilder<T> GetPage(int page)       { _isPaginated = true; _page    = page;    return this; }
    public ICmsQueryBuilder<T> GetPerPage(int perPage) { _isPaginated = true; _perPage = perPage; return this; }


    public async Task<List<T>> GetListAsync()
    {
        if (_apiService == null || _storageService == null)
            return [];

        if (_isPaginated && (_page < 1 || _perPage < 1))
            return [];

        var cacheKey = BuildCacheKey();
        if (Cms.QueryCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Debug.WriteLine($"[AppAmbit.Cms] Query cache hit: {cacheKey}");
            return (List<T>)cachedResult;
        }

        bool alreadyFetched;
        lock (Cms.FetchedContentTypes)
        {
            alreadyFetched = Cms.FetchedContentTypes.Contains(_contentType);
            if (!alreadyFetched) Cms.FetchedContentTypes.Add(_contentType);
        }

        List<T> result;

        if (alreadyFetched)
        {
            result = await QueryLocalAsync().ConfigureAwait(false);
        }
        else
        {
            var cached = await QueryLocalAsync().ConfigureAwait(false);

            if (cached == null || cached.Count == 0)
            {
                await SyncRemoteAsync().ConfigureAwait(false);
                result = await QueryLocalAsync().ConfigureAwait(false);
            }
            else
            {
                _ = Task.Run(RefreshCacheInBackgroundAsync);
                result = cached;
            }
        }

        Cms.QueryCache.TryAdd(cacheKey, result);
        Debug.WriteLine($"[AppAmbit.Cms] Query cached: {cacheKey} ({result.Count} items)");

        return result;
    }

    private string BuildCacheKey()
    {
        return $"{_contentType}|{_sqlClause}|{string.Join(",", _selectionArgs)}|{_orderByClause ?? ""}|{PaginationLimit},{PaginationOffset}";
    }

    private void AppendClause(string fragment, string arg)
    {
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append(fragment);
        _selectionArgs.Add(arg);
    }

    private static string NormalizeBool(string value) =>
        value.Equals("true",  StringComparison.OrdinalIgnoreCase) ? "1" :
        value.Equals("false", StringComparison.OrdinalIgnoreCase) ? "0" : value;

    private ICmsQueryBuilder<T> AddCondition(string field, string op, string value)
    {
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append($"CAST(json_extract(e.value, '$.{field}') AS TEXT) {op} ?");
        _selectionArgs.Add(NormalizeBool(value));
        return this;
    }

    private ICmsQueryBuilder<T> AddNumericCondition(string field, string op, object value)
    {
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append($"CAST(json_extract(e.value, '$.{field}') AS REAL) {op} ?");
        _selectionArgs.Add(value?.ToString() ?? "0");
        return this;
    }

    private ICmsQueryBuilder<T> AddListCondition(string field, bool exists, IEnumerable<string> values)
    {
        var list = values.Select(NormalizeBool).ToList();
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append(StorageService.BuildListClause(field, exists, list.Count));
        _selectionArgs.AddRange(list);
        return this;
    }

    private ICmsQueryBuilder<T> SetOrderBy(string field, string direction)
    {
        _orderByClause =
            $"CAST(json_extract(e.value, '$.{field}') AS REAL) {direction}, " +
            $"CAST(json_extract(e.value, '$.{field}') AS TEXT) COLLATE NOCASE {direction}";
        return this;
    }

    private Task<List<T>> QueryLocalAsync() =>
        QueryLocalAsync(PaginationLimit, PaginationOffset);

    private async Task<List<T>> QueryLocalAsync(int limit, int offset)
    {
        try
        {
            var dataJsonList = await _storageService!.QueryCmsDataAsync(
                _contentType,
                _sqlClause.Length > 0 ? _sqlClause.ToString() : null,
                _selectionArgs.Count > 0 ? _selectionArgs.ToArray() : null,
                _orderByClause,
                limit,
                offset).ConfigureAwait(false);

            var items = new List<T>();
            foreach (var json in dataJsonList)
            {
                try
                {
                    var item = JsonConvert.DeserializeObject<T>(json, _deserializeSettings);
                    if (item != null) items.Add(item);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppAmbit.Cms] Skipped item — deserialization failed: {ex.Message}");
                }
            }
            return items;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Cms] QueryLocalCache failed: {ex.Message}");
            return [];
        }
    }

    private async Task SyncRemoteAsync()
    {
        try
        {
            var remoteJson = await FetchAllRemoteDataAsync().ConfigureAwait(false);
            if (remoteJson != null)
                await _storageService!.UpsertCmsEntryIfChangedAsync(_contentType, remoteJson).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Cms] Sync failed: {ex.Message}");
        }
    }

    private async Task RefreshCacheInBackgroundAsync()
    {
        var sem = Cms.GetRefreshLock(_contentType);
        if (!await sem.WaitAsync(0).ConfigureAwait(false)) return;

        try
        {
            var remoteJson = await FetchAllRemoteDataAsync().ConfigureAwait(false);
            if (remoteJson != null)
                await _storageService!.UpsertCmsEntryIfChangedAsync(_contentType, remoteJson).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Cms] Background refresh failed: {ex.Message}");
        }
        finally
        {
            sem.Release();
        }
    }


    private async Task<string?> FetchAllRemoteDataAsync()
    {
        try
        {
            const int fetchPageSize = 20;

            var firstResult = await _apiService!
                .ExecuteRequest<JObject>(new CmsEndpoint(_contentType, 1, fetchPageSize))
                .ConfigureAwait(false);

            if (firstResult?.Data == null) return null;

            var firstJson = firstResult.Data;
            if (!firstJson.ContainsKey("data")) return firstJson.ToString();

            var allData = firstJson["data"] as JArray ?? [];

            var lastPage = firstJson.SelectToken("meta.last_page")?.Value<int>() ?? 1;

            for (int p = 2; p <= lastPage; p++)
            {
                var next = await _apiService!
                    .ExecuteRequest<JObject>(new CmsEndpoint(_contentType, p, fetchPageSize))
                    .ConfigureAwait(false);

                if (next?.Data?["data"] is JArray nextData)
                    foreach (var item in nextData) allData.Add(item);
            }

            firstJson["data"] = allData;
            return firstJson.ToString(Formatting.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Cms] FetchAllRemoteData failed: {ex.Message}");
            return null;
        }
    }
}
