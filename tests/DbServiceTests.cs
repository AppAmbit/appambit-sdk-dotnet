using AppAmbit.Enums;
using AppAmbit.Models.Db;
using AppAmbit.Models.Responses;
using AppAmbit.Services;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using Moq;
using Xunit;

namespace AppAmbitTest;

[Collection("DbService Tests")]
public class DbServiceTests
{
    private readonly Mock<IAPIService> _mockApi;
    private readonly DbService _service;

    public DbServiceTests()
    {
        _mockApi = new Mock<IAPIService>();
        _service = new DbService(_mockApi.Object);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void SetupQueryResponse(DbApiResponse response) =>
        _mockApi
            .Setup(s => s.ExecuteRequest<DbApiResponse>(It.IsAny<DbQueryEndpoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<DbApiResponse>.Success(response));

    private void SetupBatchResponse(DbApiResponse response) =>
        _mockApi
            .Setup(s => s.ExecuteRequest<DbApiResponse>(It.IsAny<DbBatchEndpoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<DbApiResponse>.Success(response));

    private void SetupQueryError(ApiErrorType errorType, string message) =>
        _mockApi
            .Setup(s => s.ExecuteRequest<DbApiResponse>(It.IsAny<DbQueryEndpoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<DbApiResponse>.Fail(errorType, message));

    private static DbApiResponse MakeResponse(params DbResultDto[] dtos) =>
        new() { Results = dtos.ToList() };

    private static DbResultDto MakeDto(
        List<string>? columns = null,
        List<List<object?>>? rows = null,
        int rowsRead = 0,
        int rowsWritten = 0,
        string? error = null) =>
        new()
        {
            Columns = columns ?? new(),
            Rows = rows ?? new(),
            RowsRead = rowsRead,
            RowsWritten = rowsWritten,
            Error = error
        };

    // ── QueryAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_SuccessResponse_ReturnsFirstResult()
    {
        SetupQueryResponse(MakeResponse(MakeDto(
            columns: new() { "id", "title" },
            rows: new() { new List<object?> { 1, "Buy milk" } },
            rowsRead: 1)));

        var result = await _service.QueryAsync("SELECT * FROM tasks");

        Assert.False(result.HasError);
        Assert.Equal(1, result.RowsRead);
        Assert.Equal(2, result.Columns.Count);
        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task QueryAsync_UsesDbApiResponseType_NotRawString()
    {
        // This is the root-cause regression test.
        // Before the fix, DbService called ExecuteRequest<string>, which caused
        // APIService.TryDeserializeJson<string> to throw "Could not parse JSON."
        // Now it calls ExecuteRequest<DbApiResponse> — verify the correct type is used.
        SetupQueryResponse(MakeResponse(MakeDto(rowsWritten: 1)));

        await _service.QueryAsync("INSERT INTO t VALUES (1)");

        _mockApi.Verify(s => s.ExecuteRequest<DbApiResponse>(
            It.IsAny<DbQueryEndpoint>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Confirm ExecuteRequest<string> was NEVER called
        _mockApi.Verify(s => s.ExecuteRequest<string>(
            It.IsAny<IEndpoint>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_ApiNetworkError_ReturnsDbResultWithError()
    {
        SetupQueryError(ApiErrorType.NetworkUnavailable, "No internet available");

        var result = await _service.QueryAsync("SELECT 1");

        Assert.True(result.HasError);
        // CODE-07: raw API message sanitized — user sees safe message, raw msg in Debug.WriteLine
        Assert.Contains("Network unavailable", result.Error);
    }

    [Fact]
    public async Task QueryAsync_ApiReturnsNull_ReturnsDbResultWithError()
    {
        _mockApi
            .Setup(s => s.ExecuteRequest<DbApiResponse>(It.IsAny<DbQueryEndpoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiResult<DbApiResponse>?)null);

        var result = await _service.QueryAsync("SELECT 1");

        Assert.True(result.HasError);
    }

    [Fact]
    public async Task QueryAsync_EmptyResults_ReturnsErrorResult()
    {
        SetupQueryResponse(new DbApiResponse { Results = new() });

        var result = await _service.QueryAsync("SELECT 1");

        Assert.True(result.HasError);
        // CODE-07: sanitized message — no internal detail exposed
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public async Task QueryAsync_WithParams_PassesSqlCorrectly()
    {
        string? capturedSql = null;
        _mockApi
            .Setup(s => s.ExecuteRequest<DbApiResponse>(It.IsAny<DbQueryEndpoint>(), It.IsAny<CancellationToken>()))
            .Callback<IEndpoint, CancellationToken>((ep, _) =>
            {
                // Verify endpoint payload has the SQL
                var payload = ep.Payload as AppAmbit.Models.Db.DbQueryRequest;
                capturedSql = payload?.Sql;
            })
            .ReturnsAsync(ApiResult<DbApiResponse>.Success(MakeResponse(MakeDto())));

        await _service.QueryAsync("SELECT * FROM tasks WHERE id = ?", new List<object?> { 5 });

        Assert.Equal("SELECT * FROM tasks WHERE id = ?", capturedSql);
    }

    // ── BatchAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchAsync_SuccessResponse_ReturnsAllResults()
    {
        SetupBatchResponse(MakeResponse(
            MakeDto(rowsWritten: 1),
            MakeDto(rowsWritten: 1),
            MakeDto(columns: new() { "COUNT(*)" }, rows: new() { new List<object?> { 2 } }, rowsRead: 1)));

        var results = await _service.BatchAsync(new[]
        {
            DbStatement.Of("INSERT INTO t VALUES (1)"),
            DbStatement.Of("INSERT INTO t VALUES (2)"),
            DbStatement.Of("SELECT COUNT(*) FROM t")
        }, false);

        Assert.Equal(3, results.Count);
        Assert.Equal(1, results[0].RowsWritten);
        Assert.Equal(1, results[1].RowsWritten);
        Assert.Equal(1, results[2].RowsRead);
    }

    [Fact]
    public async Task BatchAsync_UsesDbApiResponseType_NotRawString()
    {
        SetupBatchResponse(MakeResponse(MakeDto(rowsWritten: 1)));

        await _service.BatchAsync(new[] { DbStatement.Of("INSERT INTO t VALUES (1)") }, false);

        _mockApi.Verify(s => s.ExecuteRequest<DbApiResponse>(
            It.IsAny<DbBatchEndpoint>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockApi.Verify(s => s.ExecuteRequest<string>(
            It.IsAny<IEndpoint>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BatchAsync_ApiNetworkError_ReturnsListWithError()
    {
        _mockApi
            .Setup(s => s.ExecuteRequest<DbApiResponse>(It.IsAny<DbBatchEndpoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<DbApiResponse>.Fail(ApiErrorType.NetworkUnavailable, "No internet available"));

        var results = await _service.BatchAsync(new[] { DbStatement.Of("INSERT INTO t VALUES (1)") }, true);

        Assert.Single(results);
        Assert.True(results[0].HasError);
    }

    [Fact]
    public async Task BatchAsync_InTransaction_UsesBatchEndpoint()
    {
        SetupBatchResponse(MakeResponse(MakeDto(rowsWritten: 1)));

        await _service.BatchAsync(new[] { DbStatement.Of("INSERT INTO t VALUES (1)") }, inTransaction: true);

        _mockApi.Verify(s => s.ExecuteRequest<DbApiResponse>(
            It.IsAny<DbBatchEndpoint>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
