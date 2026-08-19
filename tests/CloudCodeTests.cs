using AppAmbit.Enums;
using AppAmbit;
using AppAmbit.Models.CloudCode;
using AppAmbit.Services;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace AppAmbitTest;

public sealed class CloudCodeTests : IDisposable
{
    public CloudCodeTests()
    {
        CloudCode.ResetForTesting();
    }

    public void Dispose()
    {
        CloudCode.ResetForTesting();
    }

    [Fact]
    public async Task Call_WhenNotInitialized_ReturnsNotInitializedError()
    {
        var error = await Assert.ThrowsAsync<CloudCodeError>(() => CloudCode.Call("demo"));

        Assert.Equal(CloudCodeErrorCode.NotInitialized, error.Code);
    }

    [Fact]
    public async Task Call_DynamicResponse_PreservesDataHeadersAndRequestId()
    {
        var transport = new FakeTransport(_ => Snapshot(
            200,
            "{\"ok\":true,\"value\":7}",
            new Dictionary<string, string>
            {
                ["X-Request-Id"] = "request-123",
                ["X-Custom"] = "yes"
            }));
        CloudCode.Initialize(new CloudCodeService(transport));

        var result = await CloudCode.Call("cloud-demo-json-values");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("request-123", result.RequestId);
        Assert.Equal("yes", result.Headers["X-Custom"]);
        Assert.Equal("{\"ok\":true,\"value\":7}", result.RawBody);
        Assert.IsType<JObject>(result.Data);
        Assert.Contains("ok", result.Data!.ToString());
    }

    [Fact]
    public async Task Call_TypedResponse_DeserializesBody()
    {
        var transport = new FakeTransport(_ => Snapshot(201, "{\"id\":3,\"name\":\"task\"}"));
        CloudCode.Initialize(new CloudCodeService(transport));

        var result = await CloudCode.Call<TaskResponse>(
            "cloud-demo-create-task-ios",
            HttpMethodEnum.Post,
            body: new { title = "task" });

        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.Id);
        Assert.Equal("task", result.Data.Name);
    }

    [Fact]
    public async Task Call_TypedNoContent_ReturnsNullDataAndMetadata()
    {
        var transport = new FakeTransport(_ => Snapshot(
            204,
            null,
            new Dictionary<string, string> { ["X-Request-Id"] = "delete-1" }));
        CloudCode.Initialize(new CloudCodeService(transport));

        var result = await CloudCode.Call<TaskResponse>(
            "cloud-demo-delete-task-ios",
            HttpMethodEnum.Delete,
            body: new { task_id = 4 });

        Assert.Equal(204, result.StatusCode);
        Assert.Null(result.Data);
        Assert.Equal("delete-1", result.RequestId);
    }

    [Fact]
    public async Task Call_DynamicNoContent_DoesNotDeserializeBody()
    {
        var transport = new FakeTransport(_ => Snapshot(204, "not-json"));
        CloudCode.Initialize(new CloudCodeService(transport));

        var result = await CloudCode.Call("cloud-demo-response-shapes");

        Assert.Equal(204, result.StatusCode);
        Assert.Null(result.Data);
        Assert.Equal("not-json", result.RawBody);
    }

    [Fact]
    public async Task Call_HttpError_PreservesRawBodyRequestIdAndParsedBody()
    {
        var transport = new FakeTransport(_ => Snapshot(
            429,
            "{\"error\":\"quota\" ,\"request_id\":\"body-id\"}"));
        CloudCode.Initialize(new CloudCodeService(transport));

        var error = await Assert.ThrowsAsync<CloudCodeError>(() => CloudCode.Call("cloud-demo-error-response"));

        Assert.Equal(CloudCodeErrorCode.Http, error.Code);
        Assert.Equal(429, error.StatusCode);
        Assert.Equal("body-id", error.RequestId);
        Assert.Null(error.RawBody);
        Assert.Contains("quota", error.Body!.ToString());
    }

    [Theory]
    [InlineData("\"failed\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task Call_HttpError_PreservesScalarBodyAsRaw(string rawBody)
    {
        var transport = new FakeTransport(_ => Snapshot(500, rawBody));
        CloudCode.Initialize(new CloudCodeService(transport));

        var error = await Assert.ThrowsAsync<CloudCodeError>(() => CloudCode.Call("cloud-demo-error-response"));

        Assert.Equal(CloudCodeErrorCode.Http, error.Code);
        Assert.Null(error.Body);
        Assert.Equal(rawBody, error.RawBody);
    }

    [Fact]
    public async Task Call_ValidationRejectsReservedHeadersAndInvalidSlugs()
    {
        var transport = new FakeTransport(_ => Snapshot(200, "{}"));
        CloudCode.Initialize(new CloudCodeService(transport));

        var reservedHeaders = new[]
        {
            "authorization", "cookie", "host", "content-length", "content-type", "accept",
            "x-app-key", "x-request-id"
        };

        foreach (var header in reservedHeaders)
        {
            var headerError = await Assert.ThrowsAsync<CloudCodeError>(() => CloudCode.Call(
                "valid-slug",
                headers: new Dictionary<string, string> { [header] = "spoof" }));

            Assert.Equal(CloudCodeErrorCode.InvalidHeader, headerError.Code);
            Assert.Equal(header, headerError.Header);
        }

        var slugError = await Assert.ThrowsAsync<CloudCodeError>(() => CloudCode.Call("bad/slug"));

        Assert.Equal(CloudCodeErrorCode.InvalidFunction, slugError.Code);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task Call_SnapshotsBodyAndBuildsEncodedQueryAndDeleteBody()
    {
        var transport = new FakeTransport(_ => Snapshot(200, "{}"));
        CloudCode.Initialize(new CloudCodeService(transport));
        var body = new Dictionary<string, object> { ["title"] = "before" };

        var call = CloudCode.Call(
            "slug.with-tilde",
            HttpMethodEnum.Delete,
            new Dictionary<string, string>
            {
                ["z-last"] = "last",
                ["a-first"] = "a value"
            },
            body,
            new Dictionary<string, string> { ["X-Client"] = "dotnet" });
        body["title"] = "after";
        await call;

        var endpoint = Assert.IsType<CloudCodeEndpoint>(transport.LastEndpoint);
        Assert.Equal("/fn/slug.with-tilde?a-first=a%20value&z-last=last", endpoint.Url);
        Assert.Equal("{\"title\":\"before\"}", endpoint.BodyJson);
        Assert.Equal("dotnet", endpoint.CustomHeader["X-Client"]);
    }

    [Fact]
    public async Task Call_CancelledAfterTransportSnapshotDoesNotMapOrDeliver()
    {
        var transport = new DelayedTransport();
        CloudCode.Initialize(new CloudCodeService(transport));
        using var cancellation = new CancellationTokenSource();

        var call = CloudCode.Call(
            "cloud-demo-json-values",
            cancellationToken: cancellation.Token);

        cancellation.Cancel();
        transport.Complete(Snapshot(200, "{\"ok\":true}"));

        await Assert.ThrowsAsync<OperationCanceledException>(() => call);
    }

    [Theory]
    [InlineData("No internet available", CloudCodeErrorCode.NetworkUnavailable)]
    [InlineData("TLS handshake failed", CloudCodeErrorCode.Transport)]
    public async Task Call_MapsConnectivityAndTransportErrorsSeparately(
        string message,
        CloudCodeErrorCode expectedCode)
    {
        var transport = new FakeTransport(_ => throw new HttpRequestException(message));
        CloudCode.Initialize(new CloudCodeService(transport));

        var error = await Assert.ThrowsAsync<CloudCodeError>(() => CloudCode.Call("cloud-demo-http-inspector"));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void CloudCodeTimeout_IsSixtySecondsForEveryTarget()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), CloudCodeTimeout.ForCurrentTarget);
    }

    private static HttpResponseSnapshot Snapshot(
        int statusCode,
        string? rawBody,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var responseHeaders = headers ?? new Dictionary<string, string>();
        var requestId = responseHeaders
            .FirstOrDefault(pair => string.Equals(pair.Key, "X-Request-Id", StringComparison.OrdinalIgnoreCase))
            .Value;
        if (string.IsNullOrWhiteSpace(requestId) && !string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                requestId = Newtonsoft.Json.Linq.JObject.Parse(rawBody)
                    .GetValue("request_id", StringComparison.OrdinalIgnoreCase)?.ToString();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // A raw non-JSON body has no body-level request ID.
            }
        }
        return new(statusCode, responseHeaders, rawBody, requestId);
    }

    private sealed class FakeTransport : IHttpTransport
    {
        private readonly Func<IEndpoint, HttpResponseSnapshot> _handler;

        public FakeTransport(Func<IEndpoint, HttpResponseSnapshot> handler)
        {
            _handler = handler;
        }

        public int CallCount { get; private set; }

        public IEndpoint? LastEndpoint { get; private set; }

        public Task<HttpResponseSnapshot> ExecuteRawRequestAsync(
            IEndpoint endpoint,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEndpoint = endpoint;
            return Task.FromResult(_handler(endpoint));
        }
    }

    private sealed class DelayedTransport : IHttpTransport
    {
        private readonly TaskCompletionSource<HttpResponseSnapshot> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<HttpResponseSnapshot> ExecuteRawRequestAsync(
            IEndpoint endpoint,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) => completion.Task;

        public void Complete(HttpResponseSnapshot snapshot) => completion.TrySetResult(snapshot);
    }

    private sealed class TaskResponse
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }
}
