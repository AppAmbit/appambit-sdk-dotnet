using AppAmbit.Enums;
using AppAmbit.Models.CloudCode;
using AppAmbit.Services;
using AppAmbit.Services.Interfaces;

namespace AppAmbit;

public static class CloudCode
{
    private static readonly object Sync = new();
    private static CloudCodeService? _service;

    internal static void Initialize(CloudCodeService service)
    {
        lock (Sync)
        {
            _service = service;
        }
    }

    internal static void ResetForTesting()
    {
        lock (Sync)
        {
            _service = null;
        }
    }

    public static Task<CloudCodeResponse> Call(
        string function,
        CloudCodeHttpMethod method = CloudCodeHttpMethod.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var service = CurrentService();
        return service == null
            ? Task.FromException<CloudCodeResponse>(CloudCodeError.NotInitialized())
            : service.CallAsync(function, method, query, body, headers, cancellationToken);
    }

    public static Task<CloudCodeResult<T>> Call<T>(
        string function,
        CloudCodeHttpMethod method = CloudCodeHttpMethod.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var service = CurrentService();
        return service == null
            ? Task.FromException<CloudCodeResult<T>>(CloudCodeError.NotInitialized())
            : service.CallAsync<T>(function, method, query, body, headers, cancellationToken);
    }

    private static CloudCodeService? CurrentService()
    {
        lock (Sync)
        {
            return _service;
        }
    }
}
