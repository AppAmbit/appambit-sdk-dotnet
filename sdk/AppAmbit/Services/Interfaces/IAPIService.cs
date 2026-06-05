using AppAmbit.Enums;
using AppAmbit.Models.Responses;

namespace AppAmbit.Services.Interfaces;

public interface IAPIService
{
    Task<ApiResult<T>?> ExecuteRequest<T>(IEndpoint endpoint, CancellationToken cancellationToken = default) where T : notnull;

    void SetToken(string? token);

    string? GetToken();

    Task<ApiErrorType> GetNewToken();
    
}
