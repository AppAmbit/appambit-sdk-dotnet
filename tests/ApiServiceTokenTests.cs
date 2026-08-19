using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AppAmbit.Enums;
using AppAmbit.Models.Analytics;
using AppAmbit.Models.Logs;
using AppAmbit.Models.Responses;
using AppAmbit.Services;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;

namespace AppAmbitTest;

public sealed class ApiServiceTokenTests
{
    [Fact]
    public async Task MissingToken_RefreshesOnceBeforeFiveLogsAndFiveEvents()
    {
        await using var server = new ProbeServer(request =>
        {
            if (request.Path == "/consumer/token")
                return new ProbeResponse(200, "{\"id\":\"consumer\",\"token\":\"fresh-token\"}");

            if (request.Path is "/log" or "/events")
            {
                var authorized = request.Headers.TryGetValue("Authorization", out var value)
                    && value == "Bearer fresh-token";
                return authorized
                    ? new ProbeResponse(200, "{}")
                    : new ProbeResponse(403, "<html><body>forbidden</body></html>", "text/html");
            }

            return new ProbeResponse(404, "{}");
        });

        var service = CreateService(server);
        service.SetToken("");

        var logRequests = Enumerable.Range(0, 5).Select(_ => Execute<LogResponse>(
            service,
            new LogEndpoint(new Log
            {
                Message = "Sending 5 errors after an invalid token",
                Type = LogType.Error
            })
            {
                BaseUrl = server.BaseUrl
            }));

        var eventRequests = Enumerable.Range(0, 5).Select(_ => Execute<object>(
            service,
            new SendEventEndpoint(new Event
            {
                Name = "Sending 5 events after an invalid token"
            })
            {
                BaseUrl = server.BaseUrl
            }));

        var results = await Task.WhenAll(logRequests.Concat(eventRequests));

        Assert.All(results, result => Assert.Equal(ApiErrorType.None, result));
        Assert.Equal(1, server.Requests.Count(request => request.Path == "/consumer/token"));
        Assert.Equal(5, server.Requests.Count(request => request.Path == "/log"));
        Assert.Equal(5, server.Requests.Count(request => request.Path == "/events"));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task UnauthorizedRequest_RefreshesAndRetriesOnce()
    {
        var unauthorizedResponses = 0;
        await using var server = new ProbeServer(request =>
        {
            if (request.Path == "/consumer/token")
                return new ProbeResponse(200, "{\"id\":\"consumer\",\"token\":\"fresh-token\"}");

            if (request.Path == "/events")
            {
                var authorization = request.Headers.GetValueOrDefault("Authorization");
                if (authorization == "Bearer old-token" && Interlocked.Exchange(ref unauthorizedResponses, 1) == 0)
                    return new ProbeResponse(401, "{}");

                return authorization == "Bearer fresh-token"
                    ? new ProbeResponse(200, "{}")
                    : new ProbeResponse(403, "<html><body>forbidden</body></html>", "text/html");
            }

            return new ProbeResponse(404, "{}");
        });

        var service = CreateService(server);
        service.SetToken("old-token");

        var result = await service.ExecuteRequest<object>(new SendEventEndpoint(new Event
        {
            Name = "refresh-after-401"
        })
        {
            BaseUrl = server.BaseUrl
        });

        Assert.Equal(ApiErrorType.None, result?.ErrorType);
        Assert.Equal(1, server.Requests.Count(request => request.Path == "/consumer/token"));
        Assert.Equal(2, server.Requests.Count(request => request.Path == "/events"));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task UnauthorizedRequest_StopsAfterSecond401()
    {
        await using var server = new ProbeServer(request =>
        {
            if (request.Path == "/consumer/token")
                return new ProbeResponse(200, "{\"id\":\"consumer\",\"token\":\"still-invalid\"}");

            if (request.Path == "/events")
                return new ProbeResponse(401, "{}");

            return new ProbeResponse(404, "{}");
        });

        var service = CreateService(server);
        service.SetToken("old-token");

        var result = await service.ExecuteRequest<object>(new SendEventEndpoint(new Event
        {
            Name = "two-401s"
        })
        {
            BaseUrl = server.BaseUrl
        });

        Assert.Equal(ApiErrorType.Unauthorized, result?.ErrorType);
        Assert.Equal(1, server.Requests.Count(request => request.Path == "/consumer/token"));
        Assert.Equal(2, server.Requests.Count(request => request.Path == "/events"));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task ConcurrentUnauthorizedRequestsShareOneRenewal()
    {
        await using var server = new ProbeServer(request =>
        {
            if (request.Path == "/consumer/token")
                return new ProbeResponse(200, "{\"id\":\"consumer\",\"token\":\"fresh-token\"}");

            if (request.Path == "/events")
            {
                return request.Headers.GetValueOrDefault("Authorization") switch
                {
                    "Bearer old-token" => new ProbeResponse(401, "{}"),
                    "Bearer fresh-token" => new ProbeResponse(200, "{}"),
                    _ => new ProbeResponse(403, "<html><body>forbidden</body></html>", "text/html")
                };
            }

            return new ProbeResponse(404, "{}");
        });

        var service = CreateService(server);
        service.SetToken("old-token");

        var requests = Enumerable.Range(0, 5).Select(_ => Execute<object>(
            service,
            new SendEventEndpoint(new Event { Name = "concurrent-refresh" })
            {
                BaseUrl = server.BaseUrl
            }));

        var results = await Task.WhenAll(requests);

        Assert.All(results, result => Assert.Equal(ApiErrorType.None, result));
        Assert.Equal(1, server.Requests.Count(request => request.Path == "/consumer/token"));
        Assert.Equal(10, server.Requests.Count(request => request.Path == "/events"));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task ForbiddenRequest_WithExistingToken_IsTerminal()
    {
        await using var server = new ProbeServer(request => request.Path == "/events"
            ? new ProbeResponse(403, "<html><body>forbidden</body></html>", "text/html")
            : new ProbeResponse(404, "{}"));

        var service = CreateService(server);
        service.SetToken("existing-token");

        var result = await service.ExecuteRequest<object>(new SendEventEndpoint(new Event
        {
            Name = "forbidden"
        })
        {
            BaseUrl = server.BaseUrl
        });

        Assert.Equal(ApiErrorType.Unknown, result?.ErrorType);
        Assert.Equal(1, server.Requests.Count(request => request.Path == "/events"));
        Assert.DoesNotContain(server.Requests, request => request.Path == "/consumer/token");
        Assert.Empty(server.Errors);
    }

    private static APIService CreateService(ProbeServer server)
    {
        return new APIService(() => Task.FromResult(new TokenEndpoint(new AppAmbit.Models.App.ConsumerToken())
        {
            BaseUrl = server.BaseUrl
        }));
    }

    private static async Task<ApiErrorType> Execute<T>(APIService service, IEndpoint endpoint)
        where T : notnull
    {
        return (await service.ExecuteRequest<T>(endpoint))?.ErrorType ?? ApiErrorType.Unknown;
    }

    private sealed record ProbeRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers);

    private sealed record ProbeResponse(
        int StatusCode,
        string Body,
        string ContentType = "application/json");

    private sealed class ProbeServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Func<ProbeRequest, ProbeResponse> handler;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task acceptTask;

        public ProbeServer(Func<ProbeRequest, ProbeResponse> handler)
        {
            this.handler = handler;
            listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
            acceptTask = AcceptClientsAsync();
        }

        public string BaseUrl { get; }

        public ConcurrentQueue<ProbeRequest> Requests { get; } = new();

        public ConcurrentQueue<Exception> Errors { get; } = new();

        private async Task AcceptClientsAsync()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                    _ = HandleClientAsync(client);
                }
            }
            catch when (cancellation.IsCancellationRequested)
            {
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    await using var stream = client.GetStream();
                    var requestLine = await ReadLineAsync(stream, cancellation.Token);
                    if (requestLine == null)
                        return;

                    var requestParts = requestLine.Split(' ', 3);
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    while (true)
                    {
                        var line = await ReadLineAsync(stream, cancellation.Token);
                        if (string.IsNullOrEmpty(line))
                            break;

                        var separator = line.IndexOf(':');
                        if (separator > 0)
                        {
                            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
                        }
                    }

                    if (headers.TryGetValue("Content-Length", out var contentLengthText)
                        && int.TryParse(contentLengthText, out var contentLength))
                    {
                        await ReadBytesAsync(stream, contentLength, cancellation.Token);
                    }

                    var path = new Uri("http://localhost" + requestParts[1]).AbsolutePath;
                    var request = new ProbeRequest(requestParts[0], path, headers);
                    Requests.Enqueue(request);
                    var response = handler(request);
                    var body = Encoding.UTF8.GetBytes(response.Body);
                    var responseHeaders =
                        $"HTTP/1.1 {response.StatusCode} {ReasonPhrase(response.StatusCode)}\r\n" +
                        $"Content-Type: {response.ContentType}\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Connection: close\r\n\r\n";
                    var responseBytes = Encoding.ASCII.GetBytes(responseHeaders);
                    await stream.WriteAsync(responseBytes, cancellation.Token);
                    await stream.WriteAsync(body, cancellation.Token);
                }
                catch (Exception) when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Errors.Enqueue(ex);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            listener.Stop();
            try
            {
                await acceptTask;
            }
            catch (Exception) when (cancellation.IsCancellationRequested)
            {
            }
            cancellation.Dispose();
        }

        private static async Task<string?> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var line = new List<byte>();
            var buffer = new byte[1];
            while (true)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0)
                    return line.Count == 0 ? null : Encoding.ASCII.GetString(line.ToArray());
                if (buffer[0] == (byte)'\n')
                    return Encoding.ASCII.GetString(line.ToArray());
                if (buffer[0] != (byte)'\r')
                    line.Add(buffer[0]);
            }
        }

        private static async Task ReadBytesAsync(
            NetworkStream stream,
            int length,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[Math.Min(length, 4096)];
            var remaining = length;
            while (remaining > 0)
            {
                var count = await stream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                    cancellationToken);
                if (count == 0)
                    throw new EndOfStreamException();
                remaining -= count;
            }
        }

        private static string ReasonPhrase(int statusCode) => statusCode switch
        {
            200 => "OK",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            _ => "Error"
        };
    }
}
