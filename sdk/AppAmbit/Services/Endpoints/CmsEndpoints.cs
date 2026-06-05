using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints;

internal class CmsEndpoint : BaseEndpoint
{
    public override string BaseUrl => AppConstants.CmsBaseUrl;

    public CmsEndpoint(string contentType, IList<(string Key, string Value)> queryParams, bool isSearch = false)
    {
        Method = HttpMethodEnum.Get;
        var path = isSearch ? $"/{contentType}/search" : $"/{contentType}";
        Url = queryParams.Count > 0 ? $"{path}?{BuildQueryString(queryParams)}" : path;
    }

    private static string BuildQueryString(IList<(string Key, string Value)> queryParams)
    {
        // Keys contain bracket syntax (filter[field][op]) — do not encode them.
        // Only values need encoding so user-supplied strings (search terms, etc.) are safe.
        var parts = queryParams.Select(p =>
            $"{p.Key}={Uri.EscapeDataString(p.Value)}");
        return string.Join("&", parts);
    }
}
