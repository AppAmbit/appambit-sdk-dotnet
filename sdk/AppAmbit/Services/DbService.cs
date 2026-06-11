using AppAmbit.Enums;
using AppAmbit.Models.Db;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using System.Diagnostics;

namespace AppAmbit.Services;

internal sealed class DbService : IDbService
{
    private readonly IAPIService _apiService;

    public DbService(IAPIService apiService) => _apiService = apiService;

    public async Task<DbResult> QueryAsync(string sql, List<object?>? parameters = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var endpoint = new DbQueryEndpoint(sql, parameters);
        var apiResult = await _apiService.ExecuteRequest<DbApiResponse>(endpoint, ct);

        if (apiResult == null || apiResult.ErrorType != ApiErrorType.None)
        {
            var rawMsg = apiResult?.Message ?? "DB query failed";
            Debug.WriteLine($"[DbService] QueryAsync raw error: {rawMsg}");
            var userMsg = apiResult?.ErrorType == ApiErrorType.NetworkUnavailable
                ? "Network unavailable. Please check your connection."
                : "Database operation failed.";
            return new DbResult { Error = userMsg };
        }

        var first = apiResult.Data?.Results?.FirstOrDefault();
        return first?.ToDbResult() ?? new DbResult { Error = "Database operation failed." };
    }

    public async Task<List<DbResult>> BatchAsync(IEnumerable<DbStatement> statements, bool inTransaction, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var endpoint = new DbBatchEndpoint(statements, inTransaction);
        var apiResult = await _apiService.ExecuteRequest<DbApiResponse>(endpoint, ct);

        if (apiResult == null || apiResult.ErrorType != ApiErrorType.None)
        {
            var rawMsg = apiResult?.Message ?? "DB batch failed";
            Debug.WriteLine($"[DbService] BatchAsync raw error: {rawMsg}");
            var userMsg = apiResult?.ErrorType == ApiErrorType.NetworkUnavailable
                ? "Network unavailable. Please check your connection."
                : "Database operation failed.";
            return new List<DbResult> { new DbResult { Error = userMsg } };
        }

        var results = apiResult.Data?.Results;
        if (results == null || results.Count == 0)
            return new List<DbResult> { new DbResult { Error = "Database operation failed." } };

        return results.Select(r => r.ToDbResult()).ToList();
    }
}
