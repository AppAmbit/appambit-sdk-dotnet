namespace AppAmbit.Services.Interfaces;

internal sealed class HttpResponseSnapshot
{
    public HttpResponseSnapshot(
        int statusCode,
        IReadOnlyDictionary<string, string> headers,
        string? rawBody,
        string? requestId)
    {
        StatusCode = statusCode;
        Headers = headers;
        RawBody = rawBody;
        RequestId = requestId;
    }

    public int StatusCode { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public string? RawBody { get; }

    public string? RequestId { get; }
}
