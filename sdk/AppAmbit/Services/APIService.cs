using AppAmbit.Models.Logs;
using AppAmbit.Models.Responses;
using AppAmbit.Services.ExceptionsCustom;
using AppAmbit.Services.Interfaces;
using AppAmbit.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using AppAmbit.Services.Endpoints;

namespace AppAmbit.Services;

public class APIService : IAPIService, IHttpTransport
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(20);
    private string? _token;
    private Task<ApiErrorType>? tokenRenewalTask;
    private readonly object tokenRenewalLock = new();
    private readonly Func<Task<TokenEndpoint>> tokenEndpointFactory;

    public APIService() : this(null)
    {
    }

    internal APIService(Func<Task<TokenEndpoint>>? tokenEndpointFactory)
    {
        this.tokenEndpointFactory = tokenEndpointFactory ?? TokenService.CreateTokenendpoint;
    }

    public Task<ApiResult<T>?> ExecuteRequest<T>(IEndpoint endpoint, CancellationToken cancellationToken = default) where T : notnull
    {
        return ExecuteRequestCore<T>(
            endpoint,
            cancellationToken,
            DateTime.UtcNow.Add(DefaultRequestTimeout),
            allowUnauthorizedRetry: true);
    }

    private async Task<ApiResult<T>?> ExecuteRequestCore<T>(
        IEndpoint endpoint,
        CancellationToken cancellationToken,
        DateTime deadline,
        bool allowUnauthorizedRetry) where T : notnull
    {
        using var operationCts = CreateDeadlineTokenSource(cancellationToken, deadline);
        var operationToken = operationCts.Token;
        string? requestToken = null;

        try
        {
            operationToken.ThrowIfCancellationRequested();
            if (!await HasInternetConnectionAsync().WaitAsync(operationToken))
            {
                Debug.WriteLine("[APIService] Offline - Cannot send request.");
                return ApiResult<T>.Fail(ApiErrorType.NetworkUnavailable, "No internet available");
            }

            if (RequiresConsumerToken(endpoint) && string.IsNullOrWhiteSpace(GetToken()))
            {
                Debug.WriteLine("[APIService] Token missing - triggering renewal before request");
                ApiErrorType tokenRenewalResult;
                try
                {
                    tokenRenewalResult = await RenewTokenSingleFlight(operationToken, deadline);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    return ApiResult<T>.Fail(ApiErrorType.Unknown, "Request timed out");
                }
                catch (Exception ex)
                {
                    return HandleTokenRenewalException<T>(ex);
                }

                if (!IsRenewSuccess(tokenRenewalResult))
                    return HandleFailedRenewalResult<T>(tokenRenewalResult);
            }

            requestToken = GetToken();
            var httpResponse = await RequestHttp(endpoint, operationToken, Remaining(deadline));
            operationToken.ThrowIfCancellationRequested();
            var json = await httpResponse.Content.ReadAsStringAsync(operationToken);
            operationToken.ThrowIfCancellationRequested();

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ApiResult<T>.Fail(ApiErrorType.NotFound, TryExtractServerError(json) ?? "Resource not found");
            }

            CheckStatusCodeFrom(httpResponse.StatusCode, json);

            var parsed = TryDeserializeJson<T>(json);
            return ApiResult<T>.Success(parsed);
        }
        catch (UnauthorizedException)
        {
            // CMS uses X-App-Key, not Bearer token — do not trigger token renewal.
            if (endpoint is TokenEndpoint)
            {
                Debug.WriteLine("[APIService] Token renew endpoint also failed. Session and Token must be cleared");
                ClearToken();
                return ApiResult<T>.Fail(ApiErrorType.Unauthorized, "Unauthorized");
            }

            if (endpoint is RegisterEndpoint || endpoint is CmsEndpoint)
            {
                if (endpoint is CmsEndpoint)
                    return ApiResult<T>.Fail(ApiErrorType.Unauthorized, "Unauthorized");

                Debug.WriteLine("[APIService] Token renew endpoint also failed. Session and Token must be cleared");
                ClearToken();
                return default;
            }

            if (!allowUnauthorizedRetry)
                return ApiResult<T>.Fail(ApiErrorType.Unauthorized, "Unauthorized");

            if (!string.Equals(requestToken, GetToken(), StringComparison.Ordinal))
            {
                Debug.WriteLine("[APIService] Token was renewed by another request; retrying once");
                return await ExecuteRequestCore<T>(endpoint, cancellationToken, deadline, allowUnauthorizedRetry: false);
            }

            Debug.WriteLine("[APIService] Token invalid - triggering one renewal");
            ApiErrorType tokenRenewalResult;
            try
            {
                tokenRenewalResult = await RenewTokenSingleFlight(operationToken, deadline);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return ApiResult<T>.Fail(ApiErrorType.Unknown, "Request timed out");
            }
            catch (Exception ex)
            {
                return HandleTokenRenewalException<T>(ex);
            }

            if (!IsRenewSuccess(tokenRenewalResult))
                return HandleFailedRenewalResult<T>(tokenRenewalResult);

            Debug.WriteLine("[APIService] Retrying request after one token renewal");
            return await ExecuteRequestCore<T>(endpoint, cancellationToken, deadline, allowUnauthorizedRetry: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ApiResult<T>.Fail(ApiErrorType.Unknown, "Request timed out");
        }
        catch (TimeoutException)
        {
            return ApiResult<T>.Fail(ApiErrorType.Unknown, "Request timed out");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APIService] Exception during request: {ex}");
            return ApiResult<T>.Fail(ApiErrorType.Unknown, ex.Message);
        }
    }

    private Task<ApiErrorType> RenewTokenSingleFlight(CancellationToken cancellationToken, DateTime deadline)
    {
        Task<ApiErrorType> renewalTask;
        lock (tokenRenewalLock)
        {
            tokenRenewalTask ??= RenewTokenAndClearAsync(deadline);
            renewalTask = tokenRenewalTask;
        }

        return renewalTask.WaitAsync(cancellationToken);
    }

    private async Task<ApiErrorType> RenewTokenAndClearAsync(DateTime deadline)
    {
        try
        {
            return await GetNewTokenCore(CancellationToken.None, deadline);
        }
        finally
        {
            lock (tokenRenewalLock)
            {
                tokenRenewalTask = null;
            }
        }
    }

    private bool IsRenewSuccess(ApiErrorType result)
    {
        return result == ApiErrorType.None;
    }

    private ApiResult<T> HandleTokenRenewalException<T>(Exception ex)
    {
        Debug.WriteLine($"[APIService] Error while renewing token: {ex}");
        ClearToken();
        return ApiResult<T>.Fail(ApiErrorType.Unknown, "Unexpected error during token renewal");
    }

    private ApiResult<T>? HandleFailedRenewalResult<T>(ApiErrorType result)
    {
        if (result == ApiErrorType.NetworkUnavailable)
        {
            Debug.WriteLine("[APIService] Cannot retry request: no internet after token renewal");
            return ApiResult<T>.Fail(ApiErrorType.NetworkUnavailable, "No internet after token renewal");
        }

        Debug.WriteLine($"[APIService] Could not renew token. Cleaning up");
        return ApiResult<T>.Fail(result, "Token renewal failed");
    }

    public Task<ApiErrorType> GetNewToken() =>
        GetNewTokenCore(CancellationToken.None, DateTime.UtcNow.Add(DefaultRequestTimeout));

    private async Task<ApiErrorType> GetNewTokenCore(CancellationToken cancellationToken, DateTime deadline)
    {
        using var operationCts = CreateDeadlineTokenSource(cancellationToken, deadline);
        var operationToken = operationCts.Token;
        try
        {
            var tokenEndpoint = await tokenEndpointFactory().WaitAsync(operationToken);
            var tokenResponse = await ExecuteRequestCore<TokenResponse>(
                tokenEndpoint,
                operationToken,
                deadline,
                allowUnauthorizedRetry: false);

            if (tokenResponse == null)
            {
                return ApiErrorType.Unknown;
            }

            if (tokenResponse.ErrorType != ApiErrorType.None)
            {
                Debug.WriteLine($"[APIService] Token renew failed: {tokenResponse.ErrorType}");
                return tokenResponse.ErrorType;
            }

            if (tokenResponse.Data == null)
            {
                Debug.WriteLine("[APIService] Token renew failed: Data is null");
                return ApiErrorType.Unknown;
            }

            _token = tokenResponse.Data.Token;
            return ApiErrorType.None;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APIService] Exception during token renew attempt: {ex}");
        }

        return ApiErrorType.Unknown;
    }


    private void ClearToken()
    {
        Debug.WriteLine("[APIService] Session is no longer valid. Clearing token.");
        _token = null;
    }

    private async Task<HttpResponseMessage> RequestHttp(
        IEndpoint endpoint,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        HttpClient httpClient;

        // CMS uses bracket-syntax query params (filter[field][op]=value). On iOS
        // (NSUrlSessionHandler) and Android (AndroidMessageHandler/OkHttp), the
        // platform-native handlers re-encode `[` `]` to `%5B`/`%5D`, which the
        // server treats as a different (un-parsed) parameter name and silently
        // returns the unfiltered set. Use the managed SocketsHttpHandler for CMS
        // so DangerousDisablePathAndQueryCanonicalization actually preserves the
        // literal brackets on the wire.
        HttpMessageHandler handler = endpoint is CmsEndpoint
            ? new SocketsHttpHandler()
            : new HttpClientHandler();

        var loggingHandler = new LoggingHandler(handler, rethrowExceptions: true);
        httpClient = new HttpClient(loggingHandler)
        {
            Timeout = timeout,
        };

        httpClient.DefaultRequestHeaders
            .Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));

        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true
        };

        var responseMessage = await HttpResponseMessage(endpoint, httpClient, timeout, cancellationToken);
        return responseMessage;
    }

    async Task<HttpResponseSnapshot> IHttpTransport.ExecuteRawRequestAsync(
        IEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        using var operationCts = CreateDeadlineTokenSource(cancellationToken, deadline);
        var operationToken = operationCts.Token;
        operationToken.ThrowIfCancellationRequested();

        if (!await HasInternetConnectionAsync().WaitAsync(operationToken))
            throw new HttpRequestException("No internet available");

        if (RequiresConsumerToken(endpoint) && string.IsNullOrWhiteSpace(GetToken()))
        {
            var tokenRenewalResult = await RenewTokenSingleFlight(operationToken, deadline);
            operationToken.ThrowIfCancellationRequested();
            if (tokenRenewalResult != ApiErrorType.None)
            {
                if (tokenRenewalResult == ApiErrorType.NetworkUnavailable)
                    throw new HttpRequestException("No internet available");

                if (tokenRenewalResult == ApiErrorType.Unauthorized)
                {
                    return new HttpResponseSnapshot(
                        (int)HttpStatusCode.Unauthorized,
                        new Dictionary<string, string>(),
                        null,
                        null);
                }

                throw new HttpRequestException($"Token renewal failed: {tokenRenewalResult}");
            }
        }

        var retriedAfterUnauthorized = false;
        while (true)
        {
            var requestToken = GetToken();
            var remaining = Remaining(deadline);
            using var attemptCts = CreateDeadlineTokenSource(operationToken, deadline);
            using var client = CreateHttpClient(
                endpoint is CmsEndpoint ? new SocketsHttpHandler() : new HttpClientHandler(),
                remaining,
                logSensitive: false);

            AddAuthorizationHeaderIfNeeded(client, endpoint);

            using var request = BuildRawRequest(endpoint);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                attemptCts.Token);

            var rawBody = response.Content == null
                ? null
                : Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(attemptCts.Token));
            var headers = ReadHeaders(response);
            var snapshot = new HttpResponseSnapshot(
                (int)response.StatusCode,
                headers,
                string.IsNullOrEmpty(rawBody) ? null : rawBody,
                GetRequestId(headers, rawBody));

            if (response.StatusCode == HttpStatusCode.Unauthorized
                && RequiresConsumerToken(endpoint)
                && !retriedAfterUnauthorized)
            {
                retriedAfterUnauthorized = true;
                if (!string.Equals(requestToken, GetToken(), StringComparison.Ordinal)
                    || await RenewTokenForCloudCode(operationToken, deadline))
                    continue;
            }

            return snapshot;
        }
    }

    private async Task<bool> RenewTokenForCloudCode(CancellationToken cancellationToken, DateTime deadline)
    {
        var result = await RenewTokenSingleFlight(cancellationToken, deadline);
        cancellationToken.ThrowIfCancellationRequested();
        if (result == ApiErrorType.NetworkUnavailable)
            throw new HttpRequestException("No internet available");

        return result == ApiErrorType.None;
    }

    private HttpClient CreateHttpClient(HttpMessageHandler handler, TimeSpan timeout, bool logSensitive)
    {
        var loggingHandler = new LoggingHandler(handler, logSensitive, rethrowExceptions: !logSensitive);
        var httpClient = new HttpClient(loggingHandler)
        {
            Timeout = timeout,
        };

        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return httpClient;
    }

    private HttpRequestMessage BuildRawRequest(IEndpoint endpoint)
    {
        var uri = new Uri(
            endpoint.BaseUrl + endpoint.Url,
            new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });

        var method = endpoint.Method switch
        {
            HttpMethodEnum.Get => HttpMethod.Get,
            HttpMethodEnum.Post => HttpMethod.Post,
            HttpMethodEnum.Put => HttpMethod.Put,
            HttpMethodEnum.Delete => HttpMethod.Delete,
            HttpMethodEnum.Patch => new HttpMethod("PATCH"),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint.Method))
        };

        var request = new HttpRequestMessage(method, uri);
        if (endpoint is CloudCodeEndpoint cloudCodeEndpoint && cloudCodeEndpoint.BodyJson != null
            && endpoint.Method != HttpMethodEnum.Get)
        {
            request.Content = new StringContent(cloudCodeEndpoint.BodyJson, Encoding.UTF8, "application/json");
        }

        if (endpoint.CustomHeader != null)
        {
            foreach (var header in endpoint.CustomHeader)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }

    private static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
            headers[header.Key] = string.Join(",", header.Value);
        foreach (var header in response.Content.Headers)
            headers[header.Key] = string.Join(",", header.Value);
        return headers;
    }

    private static string? GetRequestId(IReadOnlyDictionary<string, string> headers, string? rawBody)
    {
        var header = headers.FirstOrDefault(pair => string.Equals(pair.Key, "X-Request-Id", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(header.Value)) return header.Value;

        if (string.IsNullOrWhiteSpace(rawBody)) return null;
        try
        {
            return JObject.Parse(rawBody).GetValue("request_id", StringComparison.OrdinalIgnoreCase)?.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool RequiresConsumerToken(IEndpoint endpoint) =>
        !endpoint.SkipAuthorization
        && endpoint is not RegisterEndpoint
        && endpoint is not TokenEndpoint
        && endpoint is not CmsEndpoint;

    private void CheckStatusCodeFrom(HttpStatusCode code, string body)
    {
        int statusCode = (int)code;

        if (IsSuccessStatusCode(statusCode))
        {
            return;
        }

        if (HttpStatusCode.Unauthorized == code)
        {
            throw new UnauthorizedException();
        }

        var serverMessage = TryExtractServerError(body);
        var detail = string.IsNullOrWhiteSpace(serverMessage) ? code.ToString() : serverMessage;
        throw new HttpRequestException($"HTTP {statusCode}: {detail}");
    }

    private static string? TryExtractServerError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var token = Newtonsoft.Json.Linq.JToken.Parse(body);
            var msg  = token.SelectToken("error.message")?.ToString();
            var code = token.SelectToken("error.code")?.ToString();
            if (string.IsNullOrWhiteSpace(msg) && string.IsNullOrWhiteSpace(code)) return null;
            return string.IsNullOrWhiteSpace(code) ? msg : $"[{code}] {msg}";
        }
        catch
        {
            return null;
        }
    }

    private bool IsSuccessStatusCode(int statusCode)
    {
        return statusCode >= 200 && statusCode < 300;
    }

    public string? GetToken()
    {
        return _token;
    }

    public void SetToken(string? token)
    {
        _token = token;
    }

    private T? TryDeserializeJson<T>(string json)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            };
            return JsonConvert.DeserializeObject<T>(json, settings);
        }
        catch (JsonException)
        {
            var exceptionMessage = "Could not parse JSON. Something went wrong.";

            throw new JsonException(exceptionMessage);
        }
    }
    private async Task<HttpResponseMessage> HttpResponseMessage(
        IEndpoint endpoint,
        HttpClient client,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        client.Timeout = timeout;
        AddAuthorizationHeaderIfNeeded(client, endpoint);

        var fullUrl = endpoint.BaseUrl + endpoint.Url;
        return await GetHttpResponseMessage(endpoint, client, fullUrl, endpoint.Payload, cancellationToken);
    }

    private void AddAuthorizationHeaderIfNeeded(HttpClient client, IEndpoint endpoint)
    {
        if (endpoint is CmsEndpoint)
        {
            var appKey = AppAmbitSdk.AppKey;
            if (!string.IsNullOrEmpty(appKey))
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-App-Key", appKey);
            client.DefaultRequestHeaders.CacheControl = null;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AppAmbit-SDK-DotNet/4.0");
            return;
        }

        var token = GetToken();
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<HttpContent> SerializePayload(object payload, IEndpoint endpoint = null)
    {
        if (payload == null)
        {
            return null;
        }

        HttpContent content;
        if (payload is Log log)
        {
            var multipartFormDataContent = SerializeToMultipartFormDataContent(log);
            content = multipartFormDataContent;

        }
        else if (payload is LogBatch logBatch)
        {
            var multipartFormDataContent = SerializeToMultipartFormDataContent(logBatch);
            content = multipartFormDataContent;
        }
        else
        {
            content = SerializeToJSONStringContent(payload);
        }
        return content;
    }

    private static HttpContent SerializeToJSONStringContent(object payload)
    {
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        var json = JsonConvert.SerializeObject(payload, settings);

        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private MultipartFormDataContent SerializeToMultipartFormDataContent(object payload)
    {
        var formData = new MultipartFormDataContent();
        formData.AddObjectToMultipartFormDataContent(payload);
        return formData;
    }

private string SerializeStringPayload(object payload)
{
    if (payload == null)
    {
        return "";
    }

    var type = payload.GetType();
    var properties = type.GetRuntimeProperties();

    var keyValuePairs = properties
        .Where(pi => pi.GetValue(payload) != null)
        .Select(pi =>
        {
            var jsonProperty = pi.GetCustomAttribute<JsonPropertyAttribute>();
            var key = jsonProperty?.PropertyName ?? pi.Name;

            var value = pi.GetValue(payload)?.ToString() ?? "";
            return $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
        });

    return string.Join("&", keyValuePairs);
}

    private string SerializedGetURL(string url, object payload)
    {
        var serializedParameters = SerializeStringPayload(payload);
        if (string.IsNullOrEmpty(serializedParameters))
        {
            return url;
        }

        return url + "?" + serializedParameters;
    }

    private async Task<HttpResponseMessage> GetHttpResponseMessage(IEndpoint endpoint, HttpClient client, string url, object payload, CancellationToken cancellationToken)
    {
        HttpResponseMessage result;
        try
        {
            switch (endpoint.Method)
            {
                case HttpMethodEnum.Get:
                    var getUrl = SerializedGetURL(url, payload);
                    var getUri = new Uri(getUrl, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });
                    var getMsg = new HttpRequestMessage(HttpMethod.Get, getUri);
                    if (endpoint is CmsEndpoint)
                    {
                        // Force HTTP/1.1: GetComponents() throws on DangerousDisable URIs, which breaks
                        // HTTP/2 :path construction and causes bracket encoding. HTTP/1.1 uses PathAndQuery
                        // directly, which preserves literal brackets needed by the server's filter parser.
                        getMsg.Version = System.Net.HttpVersion.Version11;
                        getMsg.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    }
                    result = await client.SendAsync(getMsg, cancellationToken);
                    break;
                case HttpMethodEnum.Post:
                    var payloadJson = await SerializePayload(payload, endpoint);
                    result = await client.PostAsync(url, payloadJson, cancellationToken);
                    break;
                case HttpMethodEnum.Patch:
                    var requestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = await SerializePayload(payload)
                    };
                    result = await client.SendAsync(requestMessage, cancellationToken);
                    break;
                case HttpMethodEnum.Put:
                    result = await client.PutAsync(url, await SerializePayload(payload), cancellationToken);
                    break;
                case HttpMethodEnum.Delete:
                    result = await client.DeleteAsync(url, cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw new Exception();
        }
        return result;
    }

    private Task<bool> HasInternetConnectionAsync() => NetConnectivity.HasInternetAsync();

    private static TimeSpan Remaining(DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
            throw new TimeoutException("Request timed out.");

        return remaining;
    }

    private static CancellationTokenSource CreateDeadlineTokenSource(
        CancellationToken cancellationToken,
        DateTime deadline)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = deadline - DateTime.UtcNow;
        source.CancelAfter(remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining);
        return source;
    }


}
