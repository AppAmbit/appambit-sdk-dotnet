using AppAmbit.Enums;
using AppAmbit.Models.CloudCode;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;

namespace AppAmbit.Services;

internal sealed class CloudCodeService
{
    private static readonly HashSet<string> ReservedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "cookie", "host", "content-length", "content-type", "accept",
        "x-app-key", "x-request-id"
    };

    private readonly IHttpTransport _transport;

    internal CloudCodeService(IHttpTransport transport)
    {
        _transport = transport;
    }

    internal Task<CloudCodeResponse> CallAsync(
        string function,
        HttpMethodEnum method = HttpMethodEnum.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(function, method, query, headers);
        if (validation != null) return Task.FromException<CloudCodeResponse>(validation);

        CloudCodeEndpoint endpoint;
        try
        {
            endpoint = new CloudCodeEndpoint(function, method, query, body, headers);
        }
        catch (Exception error)
        {
            return Task.FromException<CloudCodeResponse>(CloudCodeError.InvalidBody(error));
        }

        return ExecuteAsync(endpoint, cancellationToken, snapshot =>
        {
            var data = snapshot.StatusCode == 204 ? null : DeserializeDynamic(snapshot.RawBody);
            return new CloudCodeResponse(data, snapshot.StatusCode, snapshot.Headers, snapshot.RawBody, snapshot.RequestId);
        });
    }

    internal Task<CloudCodeResult<T>> CallAsync<T>(
        string function,
        HttpMethodEnum method = HttpMethodEnum.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (typeof(T) == typeof(void))
            return Task.FromException<CloudCodeResult<T>>(CloudCodeError.InvalidResponseType());

        var validation = Validate(function, method, query, headers);
        if (validation != null) return Task.FromException<CloudCodeResult<T>>(validation);

        CloudCodeEndpoint endpoint;
        try
        {
            endpoint = new CloudCodeEndpoint(function, method, query, body, headers);
        }
        catch (Exception error)
        {
            return Task.FromException<CloudCodeResult<T>>(CloudCodeError.InvalidBody(error));
        }

        return ExecuteAsync(endpoint, cancellationToken, snapshot =>
        {
            T? data = default;
            if (snapshot.StatusCode != 204 && !string.IsNullOrWhiteSpace(snapshot.RawBody))
            {
                try
                {
                    data = JsonConvert.DeserializeObject<T>(snapshot.RawBody!, new JsonSerializerSettings
                    {
                        DateParseHandling = DateParseHandling.None
                    });
                }
                catch (Exception error)
                {
                    throw CloudCodeError.Decoding(error.Message, snapshot.RawBody, error);
                }
            }

            return new CloudCodeResult<T>(data, snapshot.StatusCode, snapshot.Headers, snapshot.RawBody, snapshot.RequestId);
        });
    }

    private async Task<T> ExecuteAsync<T>(
        CloudCodeEndpoint endpoint,
        CancellationToken cancellationToken,
        Func<HttpResponseSnapshot, T> mapper)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CloudCodeTimeout.ForCurrentTarget);

        HttpResponseSnapshot snapshot;
        try
        {
            snapshot = await _transport.ExecuteRawRequestAsync(
                endpoint,
                CloudCodeTimeout.ForCurrentTarget,
                timeoutCts.Token);
            timeoutCts.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException error)
        {
            throw CloudCodeError.TimedOut(error);
        }
        catch (TimeoutException error)
        {
            throw CloudCodeError.TimedOut(error);
        }
        catch (UriFormatException error)
        {
            throw CloudCodeError.InvalidUrl(error);
        }
        catch (HttpRequestException error)
        {
            throw IsNetworkUnavailable(error)
                ? CloudCodeError.NetworkUnavailable(error)
                : CloudCodeError.Transport(error.Message, error);
        }
        catch (CloudCodeError)
        {
            throw;
        }
        catch (Exception error)
        {
            throw CloudCodeError.Transport(error.Message, error);
        }

        timeoutCts.Token.ThrowIfCancellationRequested();
        RequireSuccess(snapshot);
        timeoutCts.Token.ThrowIfCancellationRequested();
        return mapper(snapshot);
    }

    private static CloudCodeError? Validate(
        string function,
        HttpMethodEnum method,
        IReadOnlyDictionary<string, string>? query,
        IReadOnlyDictionary<string, string>? headers)
    {
        if (string.IsNullOrEmpty(function)
            || function.Contains('/')
            || function.Any(char.IsWhiteSpace)
            || function.Any(char.IsControl))
            return CloudCodeError.InvalidFunction(function);

        if (!Enum.IsDefined(method)) return CloudCodeError.InvalidMethod();

        if (query != null)
        {
            foreach (var pair in query)
            {
                if (string.IsNullOrEmpty(pair.Key)
                    || pair.Value == null
                    || pair.Key.Any(char.IsControl)
                    || pair.Value.Any(char.IsControl))
                    return CloudCodeError.InvalidQuery(pair.Key);
            }
        }

        if (headers != null)
        {
            foreach (var pair in headers)
            {
                if (string.IsNullOrEmpty(pair.Key)
                    || pair.Value == null
                    || pair.Key.Any(char.IsControl)
                    || pair.Value.Any(char.IsControl)
                    || ReservedHeaders.Contains(pair.Key))
                    return CloudCodeError.InvalidHeader(pair.Key);
            }
        }

        return null;
    }

    private static void RequireSuccess(HttpResponseSnapshot snapshot)
    {
        if (snapshot.StatusCode is >= 200 and < 300) return;

        object? body = null;
        var rawBody = snapshot.RawBody;
        if (!string.IsNullOrWhiteSpace(snapshot.RawBody))
        {
            try
            {
                var parsed = DeserializeDynamic(snapshot.RawBody);
                if (parsed is JObject or JArray)
                {
                    body = parsed;
                    rawBody = null;
                }
            }
            catch (CloudCodeError)
            {
                // Preserve the raw body when the server returned non-JSON content.
            }
        }

        throw CloudCodeError.Http(
            snapshot.StatusCode,
            body,
            rawBody,
            snapshot.Headers,
            snapshot.RequestId);
    }

    private static object? DeserializeDynamic(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;

        try
        {
            return JToken.Parse(rawBody);
        }
        catch (Exception error)
        {
            throw CloudCodeError.Decoding(error.Message, rawBody, error);
        }
    }

    private static bool IsNetworkUnavailable(HttpRequestException error)
    {
        if (string.Equals(error.Message, "No internet available", StringComparison.OrdinalIgnoreCase))
            return true;

        return error.InnerException is SocketException socket
            && socket.SocketErrorCode is
                SocketError.HostNotFound or
                SocketError.HostUnreachable or
                SocketError.NetworkDown or
                SocketError.NetworkUnreachable or
                SocketError.AddressNotAvailable or
                SocketError.ConnectionRefused;
    }
}

internal static class CloudCodeTimeout
{
    internal static TimeSpan ForCurrentTarget => TimeSpan.FromSeconds(60);
}
