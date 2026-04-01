using AppAmbit.Models.Cms;
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
    private static IAPIService? ApiService => Cms.ApiService;
    private static IStorageService? StorageService => Cms.StorageService;

    private readonly StringBuilder _sqlClause = new();
    private readonly List<string> _selectionArgs = new();
    private string? _orderByClause;
    private bool _isPaginated;
    private int _page = 1;
    private int _perPage = 20;

    internal CmsQueryBuilder(string contentType)
    {
        _contentType = contentType;
    }

    public ICmsQueryBuilder<T> Search(string query)
    {
        var trimmed = query?.Trim() ?? "";
        if (!string.IsNullOrEmpty(trimmed))
        {
            if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
            _sqlClause.Append("value LIKE ?");
            _selectionArgs.Add($"%{trimmed}%");
        }
        return this;
    }

    public ICmsQueryBuilder<T> Equals(string field, string value) => AddCondition(field, "=", value);
    public ICmsQueryBuilder<T> NotEquals(string field, string value) => AddCondition(field, "!=", value);
    public ICmsQueryBuilder<T> Contains(string field, string value) => AddCondition(field, "LIKE", $"%{value}%");
    public ICmsQueryBuilder<T> StartsWith(string field, string value) => AddCondition(field, "LIKE", $"{value}%");

    public ICmsQueryBuilder<T> GreaterThan(string field, object value) => AddNumericCondition(field, ">", value);
    public ICmsQueryBuilder<T> GreaterThanOrEqual(string field, object value) => AddNumericCondition(field, ">=", value);
    public ICmsQueryBuilder<T> LessThan(string field, object value) => AddNumericCondition(field, "<", value);
    public ICmsQueryBuilder<T> LessThanOrEqual(string field, object value) => AddNumericCondition(field, "<=", value);

    public ICmsQueryBuilder<T> InList(string field, IEnumerable<string> values)
    {
        var list = values.ToList();
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append($"json_extract(value, '$.{field}') IN (");
        for (int i = 0; i < list.Count; i++)
        {
            _sqlClause.Append(i < list.Count - 1 ? "?," : "?");
            _selectionArgs.Add(list[i]);
        }
        _sqlClause.Append(")");
        return this;
    }

    public ICmsQueryBuilder<T> NotInList(string field, IEnumerable<string> values)
    {
        var list = values.ToList();
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append($"json_extract(value, '$.{field}') NOT IN (");
        for (int i = 0; i < list.Count; i++)
        {
            _sqlClause.Append(i < list.Count - 1 ? "?," : "?");
            _selectionArgs.Add(list[i]);
        }
        _sqlClause.Append(")");
        return this;
    }

    public ICmsQueryBuilder<T> OrderByAscending(string field)
    {
        _orderByClause = $"json_extract(value, '$.{field}') ASC";
        return this;
    }

    public ICmsQueryBuilder<T> OrderByDescending(string field)
    {
        _orderByClause = $"json_extract(value, '$.{field}') DESC";
        return this;
    }

    public ICmsQueryBuilder<T> GetPage(int page) { _isPaginated = true; _page = page; return this; }
    public ICmsQueryBuilder<T> GetPerPage(int perPage) { _isPaginated = true; _perPage = perPage; return this; }

    private ICmsQueryBuilder<T> AddCondition(string field, string op, string value)
    {
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append($"json_extract(value, '$.{field}') {op} ?");
        _selectionArgs.Add(value);
        return this;
    }

    private ICmsQueryBuilder<T> AddNumericCondition(string field, string op, object value)
    {
        if (_sqlClause.Length > 0) _sqlClause.Append(" AND ");
        _sqlClause.Append($"CAST(json_extract(value, '$.{field}') AS REAL) {op} ?");
        _selectionArgs.Add(value?.ToString() ?? "0");
        return this;
    }

    public async Task<List<T>> GetListAsync()
    {
        if (ApiService == null || StorageService == null)
            return new List<T>();

        int limit = _isPaginated ? _perPage : 0;
        int offset = _isPaginated ? (_page - 1) * _perPage : 0;

        bool isAlreadyFetched;
        lock (Cms.FetchedContentTypes)
        {
            isAlreadyFetched = Cms.FetchedContentTypes.Contains(_contentType);
            if (!isAlreadyFetched)
            {
                Cms.FetchedContentTypes.Add(_contentType);
            }
        }

        if (isAlreadyFetched)
        {
            _ = Task.Run(RefreshCacheInBackgroundAsync);
            return await Task.Run(() => QueryLocalCacheAsync(limit, offset)).ConfigureAwait(false);
        }

        var cached = await Task.Run(() => QueryLocalCacheAsync(limit, offset)).ConfigureAwait(false);

        if (cached == null || cached.Count == 0)
        {
            await FetchRemoteDataSyncAsync().ConfigureAwait(false);
            return await Task.Run(() => QueryLocalCacheAsync(limit, offset)).ConfigureAwait(false);
        }
        else
        {
            _ = Task.Run(RefreshCacheInBackgroundAsync);
            return cached;
        }
    }

    private async Task<List<T>> QueryLocalCacheAsync(int limit, int offset)
    {
        try
        {
            var dataJsonList = await StorageService!.QueryCmsDataAsync(
                _contentType,
                _sqlClause.Length > 0 ? _sqlClause.ToString() : null,
                _selectionArgs.Count > 0 ? _selectionArgs.ToArray() : null,
                _orderByClause,
                limit,
                offset).ConfigureAwait(false);

            var items = new List<T>();
            foreach (var json in dataJsonList)
            {
                var item = JsonConvert.DeserializeObject<T>(json);
                if (item != null) items.Add(item);
            }
            return items;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Cms] QueryLocalCache failed: {ex.Message}");
            return new List<T>();
        }
    }

    private async Task FetchRemoteDataSyncAsync()
    {
        try
        {
            var remoteJson = await FetchAllRemoteDataAsync().ConfigureAwait(false);
            if (remoteJson != null)
            {
                var localEntity = await StorageService!.GetCmsEntryAsync(_contentType).ConfigureAwait(false);
                if (CheckIfDataChanged(localEntity?.JsonData, remoteJson))
                {
                    await StorageService!.UpsertCmsEntryAsync(new CmsCacheEntity
                    {
                        ContentType = _contentType,
                        JsonData = remoteJson,
                        LastSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }).ConfigureAwait(false);
                }
            }
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
            {
                var localEntity = await StorageService!.GetCmsEntryAsync(_contentType).ConfigureAwait(false);
                if (CheckIfDataChanged(localEntity?.JsonData, remoteJson))
                {
                    await StorageService!.UpsertCmsEntryAsync(new CmsCacheEntity
                    {
                        ContentType = _contentType,
                        JsonData = remoteJson,
                        LastSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }).ConfigureAwait(false);
                }
            }
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

    private bool CheckIfDataChanged(string? localJson, string remoteJson)
    {
        if (localJson == null) return true;
        if (localJson.Length != remoteJson.Length) return true;
        return !string.Equals(localJson, remoteJson, StringComparison.Ordinal);
    }

    private async Task<string?> FetchAllRemoteDataAsync()
    {
        try
        {
            int page = 1;
            const int perPage = 20;

            var firstEndpoint = new CmsEndpoint(_contentType, page, perPage);
            var firstResult = await ApiService!.ExecuteRequest<JObject>(firstEndpoint).ConfigureAwait(false);

            if (firstResult == null) return null;

            bool isNotFound = firstResult.ErrorType == Enums.ApiErrorType.NotFound;
            bool isEmpty = firstResult.Data != null 
                           && firstResult.Data["data"] is JArray arr 
                           && arr.Count == 0 
                           && firstResult.Data["meta"]?["total"]?.Value<int>() == 0;

            if (isNotFound || isEmpty)
            {
                await StorageService!.DeleteCmsEntryAsync(_contentType).ConfigureAwait(false);
                return null;
            }

            if (firstResult.Data == null) return null;

            var firstJson = firstResult.Data;
            if (!firstJson.ContainsKey("data")) return firstJson.ToString();

            var allData = firstJson["data"] as JArray ?? new JArray();

            if (firstJson.ContainsKey("meta"))
            {
                var meta = firstJson["meta"]!;
                int total = meta["total"]?.Value<int>() ?? 0;
                int totalPages = (int)Math.Ceiling((double)total / perPage);

                for (int p = 2; p <= totalPages; p++)
                {
                    var nextEndpoint = new CmsEndpoint(_contentType, p, perPage);
                    var nextResult = await ApiService!.ExecuteRequest<JObject>(nextEndpoint).ConfigureAwait(false);
                    if (nextResult?.Data?["data"] is JArray nextData)
                    {
                        foreach (var item in nextData)
                            allData.Add(item);
                    }
                }
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
