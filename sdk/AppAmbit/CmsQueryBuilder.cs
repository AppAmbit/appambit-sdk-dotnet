using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace AppAmbit;

public class CmsQueryBuilder<T> : ICmsQueryBuilder<T> where T : class
{
    // Single-value query params (sort, q, page, per_page) — last write wins so chained
    // calls like .OrderByAscending(...).OrderByDescending(...) behave intuitively.
    private const string ParamSort    = "sort";
    private const string ParamSearch  = "q";
    private const string ParamPage    = "page";
    private const string ParamPerPage = "per_page";

    private readonly string _contentType;
    private static IAPIService? _apiService => Cms.ApiService;

    // filter[...] params can repeat (multiple distinct filters), kept in insertion order.
    private readonly List<(string Key, string Value)> _filterParams = new();
    // Single-value params keyed by name; later sets overwrite.
    private readonly Dictionary<string, string> _singleParams = new();
    private bool _isSearch;
    private bool _userSpecifiedPaging;

    private static readonly JsonSerializerSettings _deserializeSettings = new()
    {
        Error = (_, args) => args.ErrorContext.Handled = true,
        NullValueHandling = NullValueHandling.Ignore
    };

    private static readonly JsonSerializer _jsonSerializer = JsonSerializer.Create(_deserializeSettings);

    internal CmsQueryBuilder(string contentType)
    {
        _contentType = contentType;
    }

    public ICmsQueryBuilder<T> Search(string query)
    {
        var trimmed = query?.Trim() ?? "";
        if (!string.IsNullOrEmpty(trimmed))
        {
            _isSearch = true;
            _singleParams[ParamSearch] = trimmed;
        }
        return this;
    }

    public ICmsQueryBuilder<T> Equals(string field, string value)
        => AddFilter($"filter[{field}]", value);

    public ICmsQueryBuilder<T> NotEquals(string field, string value)
        => AddFilter($"filter[{field}][neq]", value);

    public ICmsQueryBuilder<T> Contains(string field, string value)
        => AddFilter($"filter[{field}][contains]", value);

    public ICmsQueryBuilder<T> StartsWith(string field, string value)
        => AddFilter($"filter[{field}][starts_with]", value);

    public ICmsQueryBuilder<T> GreaterThan(string field, object value)
        => AddFilter($"filter[{field}][gt]", value?.ToString() ?? "");

    public ICmsQueryBuilder<T> GreaterThanOrEqual(string field, object value)
        => AddFilter($"filter[{field}][gte]", value?.ToString() ?? "");

    public ICmsQueryBuilder<T> LessThan(string field, object value)
        => AddFilter($"filter[{field}][lt]", value?.ToString() ?? "");

    public ICmsQueryBuilder<T> LessThanOrEqual(string field, object value)
        => AddFilter($"filter[{field}][lte]", value?.ToString() ?? "");

    public ICmsQueryBuilder<T> InList(string field, IEnumerable<string> values)
        => AddFilter($"filter[{field}][in]", string.Join(",", values));

    public ICmsQueryBuilder<T> NotInList(string field, IEnumerable<string> values)
        => AddFilter($"filter[{field}][nin]", string.Join(",", values));

    public ICmsQueryBuilder<T> OrderByAscending(string field)
    {
        _singleParams[ParamSort] = field;
        return this;
    }

    public ICmsQueryBuilder<T> OrderByDescending(string field)
    {
        _singleParams[ParamSort] = $"-{field}";
        return this;
    }

    public ICmsQueryBuilder<T> GetPage(int page)
    {
        _userSpecifiedPaging = true;
        _singleParams[ParamPage] = page.ToString();
        return this;
    }

    public ICmsQueryBuilder<T> GetPerPage(int perPage)
    {
        _userSpecifiedPaging = true;
        _singleParams[ParamPerPage] = perPage.ToString();
        return this;
    }

    private ICmsQueryBuilder<T> AddFilter(string key, string value)
    {
        _filterParams.Add((key, value));
        return this;
    }

    public async Task<List<T>> GetListAsync(CancellationToken cancellationToken = default)
    {
        if (_apiService == null)
            throw new InvalidOperationException(
                $"[AppAmbit.Cms] SDK not initialized — call AppAmbitSdk.Start() before using Cms.Content('{_contentType}').");

        // If the caller didn't paginate, auto-fetch every page using meta.last_page.
        // Otherwise honor exactly what they asked for and return one page.
        if (_userSpecifiedPaging || _isSearch)
            return await FetchSinglePageAsync(cancellationToken).ConfigureAwait(false);

        return await FetchAllPagesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<T>> FetchSinglePageAsync(CancellationToken cancellationToken)
    {
        var json = await ExecuteAsync(BuildParams(pageOverride: null, perPageOverride: null), cancellationToken)
            .ConfigureAwait(false);
        return ExtractItems(json);
    }

    private async Task<List<T>> FetchAllPagesAsync(CancellationToken cancellationToken)
    {
        var firstJson = await ExecuteAsync(BuildParams(pageOverride: 1, perPageOverride: null), cancellationToken)
            .ConfigureAwait(false);
        var items = ExtractItems(firstJson);

        var lastPage = firstJson?.SelectToken("meta.last_page")?.Value<int>() ?? 1;
        for (var p = 2; p <= lastPage; p++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageJson = await ExecuteAsync(BuildParams(pageOverride: p, perPageOverride: null), cancellationToken)
                .ConfigureAwait(false);
            items.AddRange(ExtractItems(pageJson));
        }

        return items;
    }

    private List<(string Key, string Value)> BuildParams(int? pageOverride, int? perPageOverride)
    {
        // Order: page/per_page first (matches server-side expectations and CDN cache keying),
        // then sort/q, then filter[...] params.
        var result = new List<(string Key, string Value)>();

        if (pageOverride.HasValue)
            result.Add((ParamPage, pageOverride.Value.ToString()));
        else if (_singleParams.TryGetValue(ParamPage, out var p))
            result.Add((ParamPage, p));

        if (perPageOverride.HasValue)
            result.Add((ParamPerPage, perPageOverride.Value.ToString()));
        else if (_singleParams.TryGetValue(ParamPerPage, out var pp))
            result.Add((ParamPerPage, pp));

        if (_singleParams.TryGetValue(ParamSort, out var sort))
            result.Add((ParamSort, sort));

        if (_singleParams.TryGetValue(ParamSearch, out var q))
            result.Add((ParamSearch, q));

        result.AddRange(_filterParams);
        return result;
    }

    private async Task<JObject?> ExecuteAsync(
        IList<(string Key, string Value)> queryParams,
        CancellationToken cancellationToken)
    {
        var endpoint = new CmsEndpoint(_contentType, queryParams, _isSearch);
        var result = await _apiService!.ExecuteRequest<JObject>(endpoint, cancellationToken).ConfigureAwait(false);

        if (result == null || result.ErrorType != Enums.ApiErrorType.None)
        {
            var errorType = result?.ErrorType ?? Enums.ApiErrorType.Unknown;
            var detail    = string.IsNullOrWhiteSpace(result?.Message) ? errorType.ToString() : result.Message;
            throw new HttpRequestException(
                $"[AppAmbit.Cms] Failed to fetch '{_contentType}': {detail}.");
        }

        return result.Data;
    }

    private List<T> ExtractItems(JObject? json)
    {
        if (json?["data"] is not JArray dataArray)
            return [];

        var items = new List<T>();
        foreach (var token in dataArray)
        {
            try
            {
                var item = token.ToObject<T>(_jsonSerializer);
                if (item != null) items.Add(item);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppAmbit.Cms] Skipped item — deserialization failed: {ex.Message}");
            }
        }
        return items;
    }
}
