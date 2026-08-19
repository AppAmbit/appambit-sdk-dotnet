using AppAmbit.Models.CloudCode;
using AppAmbit.Services.Interfaces;

namespace AppAmbitAvalonia;

public static class CloudCode
{
    public static Task<CloudCodeResponse> Call(
        string function,
        HttpMethodEnum method = HttpMethodEnum.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) =>
        AppAmbit.CloudCode.Call(function, method, query, body, headers, cancellationToken);

    public static Task<CloudCodeResult<T>> Call<T>(
        string function,
        HttpMethodEnum method = HttpMethodEnum.Post,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) =>
        AppAmbit.CloudCode.Call<T>(function, method, query, body, headers, cancellationToken);
}
