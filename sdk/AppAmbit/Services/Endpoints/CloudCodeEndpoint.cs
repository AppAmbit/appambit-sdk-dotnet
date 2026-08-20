using AppAmbit.Enums;
using AppAmbit.Models.CloudCode;
using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AppAmbit.Services.Endpoints;

internal sealed class CloudCodeEndpoint : BaseEndpoint
{
    public CloudCodeEndpoint(
        string function,
        CloudCodeHttpMethod method,
        IReadOnlyDictionary<string, string>? query,
        object? body,
        IReadOnlyDictionary<string, string>? headers)
    {
        Function = function;
        Url = BuildPath(function, query);
        Method = ToTransportMethod(method);
        Payload = body;
        BodyJson = SerializeBody(body);
        CustomHeader = headers == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(headers);
    }

    public string Function { get; }

    public string? BodyJson { get; }

    private static HttpMethodEnum ToTransportMethod(CloudCodeHttpMethod method) => method switch
    {
        CloudCodeHttpMethod.Get => HttpMethodEnum.Get,
        CloudCodeHttpMethod.Post => HttpMethodEnum.Post,
        CloudCodeHttpMethod.Put => HttpMethodEnum.Put,
        CloudCodeHttpMethod.Delete => HttpMethodEnum.Delete,
        CloudCodeHttpMethod.Patch => HttpMethodEnum.Patch,
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    private static string? SerializeBody(object? body)
    {
        if (body == null) return null;

        var json = JsonConvert.SerializeObject(body);
        if (string.IsNullOrWhiteSpace(json))
            throw new JsonSerializationException("Cloud Code body must serialize to a JSON object.");

        var token = JToken.Parse(json);
        if (token.Type != JTokenType.Object)
            throw new JsonSerializationException("Cloud Code body must be a JSON object.");

        return json;
    }

    private static string BuildPath(string function, IReadOnlyDictionary<string, string>? query)
    {
        var path = $"/fn/{Uri.EscapeDataString(function)}";
        if (query == null || query.Count == 0) return path;

        var parameters = query.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        return $"{path}?{string.Join("&", parameters)}";
    }
}
