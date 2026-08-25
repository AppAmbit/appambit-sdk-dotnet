namespace AppAmbit.Models.CloudCode;

public sealed class CloudCodeResult<T>
{
    public CloudCodeResult(
        T? data,
        int statusCode,
        IReadOnlyDictionary<string, string> headers,
        string? rawBody,
        string? requestId)
    {
        Data = data;
        StatusCode = statusCode;
        Headers = headers;
        RawBody = rawBody;
        RequestId = requestId;
    }

    public T? Data { get; }

    public int StatusCode { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public string? RawBody { get; }

    public string? RequestId { get; }
}
