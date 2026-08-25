using AppAmbit.Models.CloudCode;
using AppAmbit.Enums;

namespace AppAmbitAvalonia;

public static class CloudCode
{
    public static Task<CloudCodeResponse> Call(
        string function,
        CloudCodeHttpMethod method = CloudCodeHttpMethod.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) =>
        AppAmbit.CloudCode.Call(function, method, query, body, headers, cancellationToken);

    public static Task<CloudCodeResult<T>> Call<T>(
        string function,
        CloudCodeHttpMethod method = CloudCodeHttpMethod.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) =>
        AppAmbit.CloudCode.Call<T>(function, method, query, body, headers, cancellationToken);
}
