using AppAmbit.Models.CloudCode;
using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;
using Newtonsoft.Json;

namespace AppAmbit.Services.Endpoints;

internal sealed class CloudCodeEndpoint : BaseEndpoint
{
    public CloudCodeEndpoint(
        string function,
        HttpMethodEnum method,
        IReadOnlyDictionary<string, string>? query,
        object? body,
        IReadOnlyDictionary<string, string>? headers)
    {
        Function = function;
        Url = BuildPath(function, query);
        Method = method;
        Payload = body;
        BodyJson = body == null ? null : JsonConvert.SerializeObject(body);
        CustomHeader = headers == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(headers);
    }

    public string Function { get; }

    public string? BodyJson { get; }

    private static string BuildPath(string function, IReadOnlyDictionary<string, string>? query)
    {
        var path = $"/fn/{Uri.EscapeDataString(function)}";
        if (query == null || query.Count == 0) return path;

        var parameters = query.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        return $"{path}?{string.Join("&", parameters)}";
    }
}
