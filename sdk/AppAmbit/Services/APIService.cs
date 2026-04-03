using AppAmbit.Models.Logs;
using AppAmbit.Models.Responses;
using AppAmbit.Services.ExceptionsCustom;
using AppAmbit.Services.Interfaces;
using AppAmbit.Enums;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using AppAmbit.Services.Endpoints;

namespace AppAmbit.Services;

public class APIService : IAPIService
{
    private string? _token;
    private Task<ApiErrorType>? currentTokenRenewalTask;

    public async Task<ApiResult<T>?> ExecuteRequest<T>(IEndpoint endpoint) where T : notnull
    {
        if (!await HasInternetConnectionAsync())
        {
            Debug.WriteLine("[APIService] Offline - Cannot send request.");
            return ApiResult<T>.Fail(ApiErrorType.NetworkUnavailable, "No internet available");
        }

        try
        {
            var httpResponse = await RequestHttp(endpoint);

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ApiResult<T>.Fail(ApiErrorType.NotFound, "Resource not found");
            }

            CheckStatusCodeFrom(httpResponse.StatusCode);

            var json = await httpResponse.Content.ReadAsStringAsync();
            var parsed = TryDeserializeJson<T>(json);
            return ApiResult<T>.Success(parsed);
        }
        catch (UnauthorizedException)
        {
            if (endpoint is RegisterEndpoint || endpoint is TokenEndpoint)
            {
                Debug.WriteLine("[APIService] Token renew endpoint also failed. Session and Token must be cleared");
                ClearToken();
                return default;
            }

            if (!IsRenewingToken())
            {
                try
                {
                    Debug.WriteLine("[APIService] Token invalid - triggering renewal");
                    currentTokenRenewalTask = GetNewToken();
                    var tokenRenewalResult = await currentTokenRenewalTask;

                    if (!IsRenewSuccess(tokenRenewalResult))
                    {
                        return HandleFailedRenewalResult<T>(tokenRenewalResult);
                    }
                }
                catch (Exception ex)
                {
                    return HandleTokenRenewalException<T>(ex);
                }
                finally
                {
                    currentTokenRenewalTask = null;
                }
            }

            Debug.WriteLine("[APIService] Retrying request after token renewal");
            return await ExecuteRequest<T>(endpoint);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[APIService] Exception during request: {ex}");
            return ApiResult<T>.Fail(ApiErrorType.Unknown, "Unexpected error during request");
        }
    }

    private bool IsRenewingToken()
    {
        return currentTokenRenewalTask != null;
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

    public async Task<ApiErrorType> GetNewToken()
    {
        try
        {
            var tokenEndpoint = await TokenService.CreateTokenendpoint();
            var tokenResponse = await ExecuteRequest<TokenResponse>(tokenEndpoint);

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

    private async Task<HttpResponseMessage> RequestHttp(IEndpoint endpoint)
    {
        HttpClient httpClient;

        var handler = new HttpClientHandler();
        var loggingHandler = new LoggingHandler(handler);
        httpClient = new HttpClient(loggingHandler)
        {
            Timeout = TimeSpan.FromMinutes(2),
        };

        httpClient.DefaultRequestHeaders
            .Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue 
        { 
            NoCache = true 
        };

        var responseMessage = await HttpResponseMessage(endpoint, httpClient);
        return responseMessage;
    }

    private void CheckStatusCodeFrom(HttpStatusCode code)
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

        throw new HttpRequestException($"HTTP error {statusCode}: {code}");
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
    private async Task<HttpResponseMessage> HttpResponseMessage(IEndpoint endpoint, HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        AddAuthorizationHeaderIfNeeded(client, endpoint);

        var fullUrl = endpoint.BaseUrl + endpoint.Url;
        return await GetHttpResponseMessage(endpoint, client, fullUrl, endpoint.Payload);
    }

    private void AddAuthorizationHeaderIfNeeded(HttpClient client, IEndpoint endpoint)
    {
        if (endpoint is CmsEndpoint)
        {
            var appKey = AppAmbitSdk.AppKey;
            if (!string.IsNullOrEmpty(appKey))
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-App-Key", appKey);
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

    private async Task<HttpResponseMessage> GetHttpResponseMessage(IEndpoint endpoint, HttpClient client, string url, object payload)
    {
        HttpResponseMessage result;
        try
        {
            switch (endpoint.Method)
            {
                case HttpMethodEnum.Get:
                    result = await client.GetAsync(SerializedGetURL(url, payload));
                    break;
                case HttpMethodEnum.Post:
                    var payloadJson = await SerializePayload(payload, endpoint);
                    result = await client.PostAsync(url, payloadJson);
                    break;
                case HttpMethodEnum.Patch:
                    var requestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = await SerializePayload(payload)
                    };
                    result = await client.SendAsync(requestMessage);
                    break;
                case HttpMethodEnum.Put:
                    result = await client.PutAsync(url, await SerializePayload(payload));
                    break;
                case HttpMethodEnum.Delete:
                    result = await client.DeleteAsync(url);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (TaskCanceledException)
        {
            throw new Exception();
        }
        return result;
    }

    private Task<bool> HasInternetConnectionAsync() => NetConnectivity.HasInternetAsync();


}
