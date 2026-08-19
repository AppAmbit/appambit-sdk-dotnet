namespace AppAmbit.Models.CloudCode;

public enum CloudCodeErrorCode
{
    NotInitialized,
    InvalidFunction,
    InvalidMethod,
    InvalidQuery,
    InvalidBody,
    InvalidHeader,
    InvalidResponseType,
    Cancelled,
    NetworkUnavailable,
    TimedOut,
    InvalidUrl,
    Transport,
    Decoding,
    Http
}

public sealed class CloudCodeError : Exception
{
    private CloudCodeError(
        CloudCodeErrorCode code,
        string message,
        string? function = null,
        string? header = null,
        int? statusCode = null,
        object? body = null,
        string? rawBody = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Function = function;
        Header = header;
        StatusCode = statusCode;
        Body = body;
        RawBody = rawBody;
        Headers = headers ?? new Dictionary<string, string>();
        RequestId = requestId;
    }

    public CloudCodeErrorCode Code { get; }

    public string? Function { get; }

    public string? Header { get; }

    public int? StatusCode { get; }

    public object? Body { get; }

    public string? RawBody { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public string? RequestId { get; }

    public static CloudCodeError NotInitialized() => new(
        CloudCodeErrorCode.NotInitialized,
        "Cloud Code is not initialized. Call AppAmbitSdk.Start() first.");

    public static CloudCodeError InvalidFunction(string? function) => new(
        CloudCodeErrorCode.InvalidFunction,
        $"Invalid Cloud Code function slug: {function}",
        function: function);

    public static CloudCodeError InvalidMethod() => new(
        CloudCodeErrorCode.InvalidMethod,
        "Cloud Code HTTP method is required.");

    public static CloudCodeError InvalidQuery(string? detail) => new(
        CloudCodeErrorCode.InvalidQuery,
        $"Invalid Cloud Code query parameter: {detail}");

    public static CloudCodeError InvalidBody(Exception cause) => new(
        CloudCodeErrorCode.InvalidBody,
        "The Cloud Code body is not valid JSON.",
        innerException: cause);

    public static CloudCodeError InvalidHeader(string? header) => new(
        CloudCodeErrorCode.InvalidHeader,
        $"The Cloud Code header is reserved or invalid and cannot be overridden: {header}",
        header: header);

    public static CloudCodeError InvalidResponseType() => new(
        CloudCodeErrorCode.InvalidResponseType,
        "Cloud Code response type is required.");

    public static CloudCodeError Cancelled() => new(
        CloudCodeErrorCode.Cancelled,
        "Cloud Code request was cancelled.");

    public static CloudCodeError NetworkUnavailable(Exception? cause = null) => new(
        CloudCodeErrorCode.NetworkUnavailable,
        "Cloud Code is unavailable because the network is offline.",
        innerException: cause);

    public static CloudCodeError TimedOut(Exception? cause = null) => new(
        CloudCodeErrorCode.TimedOut,
        "Cloud Code request timed out.",
        innerException: cause);

    public static CloudCodeError InvalidUrl(Exception? cause = null) => new(
        CloudCodeErrorCode.InvalidUrl,
        "Cloud Code URL is invalid.",
        innerException: cause);

    public static CloudCodeError Transport(string message, Exception? cause = null) => new(
        CloudCodeErrorCode.Transport,
        $"Cloud Code network request failed: {message}",
        innerException: cause);

    public static CloudCodeError Decoding(string message, string? rawBody, Exception? cause = null) => new(
        CloudCodeErrorCode.Decoding,
        $"Cloud Code response could not be decoded: {message}",
        rawBody: rawBody,
        innerException: cause);

    public static CloudCodeError Http(
        int statusCode,
        object? body,
        string? rawBody,
        IReadOnlyDictionary<string, string> headers,
        string? requestId) => new(
        CloudCodeErrorCode.Http,
        BuildHttpMessage(statusCode, rawBody, requestId),
        statusCode: statusCode,
        body: body,
        rawBody: rawBody,
        headers: headers,
        requestId: requestId);

    private static string BuildHttpMessage(int statusCode, string? rawBody, string? requestId)
    {
        var message = $"Cloud Code returned HTTP {statusCode}.";
        if (!string.IsNullOrEmpty(rawBody)) message += $" Body: {rawBody}";
        if (!string.IsNullOrEmpty(requestId)) message += $" Request ID: {requestId}";
        return message;
    }
}
