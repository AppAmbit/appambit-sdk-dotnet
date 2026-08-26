
using System.Diagnostics;

namespace AppAmbit.Services;

public class LoggingHandler : DelegatingHandler
{
    private static double _totalRequestSize = 0;
    private static readonly object _lock = new object();
    private readonly bool _logSensitive;
    private readonly bool _rethrowExceptions;

    public static double TotalRequestSize
    {
        get
        {
            lock (_lock)
            {
                return _totalRequestSize;
            }
        }
    }

    public LoggingHandler(
        HttpMessageHandler innerHandler,
        bool logSensitive = true,
        bool rethrowExceptions = false)
        : base(innerHandler)
    {
        _logSensitive = logSensitive;
        _rethrowExceptions = rethrowExceptions;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestTarget = _logSensitive
                ? request.RequestUri?.AbsoluteUri
                : request.RequestUri?.GetLeftPart(UriPartial.Path);
            Debug.WriteLine($"[APIService] --> {request.Method} {requestTarget} (HTTP/{request.Version})");
            if (_logSensitive)
            {
                foreach (var h in request.Headers)
                    Debug.WriteLine($"[APIService]    {h.Key}: {string.Join(", ", h.Value)}");
            }

            if (_logSensitive && request.Content != null)
            {
                var reqBody = await request.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine(reqBody);
            }

            var response = await base.SendAsync(request, cancellationToken);
            await CalculateRequestSize(request);

            if (_logSensitive)
            {
                var xCache = response.Headers.TryGetValues("X-Cache", out var xcValues) ? string.Join(",", xcValues) : "n/a";
                var cfId = response.Headers.TryGetValues("X-Amz-Cf-Id", out var cfValues) ? string.Join(",", cfValues) : "n/a";
                Debug.WriteLine($"[APIService] <-- {(int)response.StatusCode} | X-Cache: {xCache} | CF-Id: {cfId}");
            }
            else
            {
                Debug.WriteLine($"[APIService] <-- {(int)response.StatusCode}");
            }

            if (_logSensitive && response.Content != null)
            {
                var respBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine(respBody);
            }
            return response;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (_rethrowExceptions) throw;
        }
        return null!;
    }

    internal static async Task CalculateRequestSize(HttpRequestMessage request)
    {
        int bodySize = 0;

        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            bodySize = content.Length;
        }

        lock (_lock)
        {
            _totalRequestSize += bodySize;
        }

        Debug.WriteLine($"[APIService] - Request Size: {bodySize} bytes, Total: {_totalRequestSize:F4} bytes");
    }

    public static void ResetTotalSize()
    {
        lock (_lock)
        {
            _totalRequestSize = 0;
        }
    }
}
