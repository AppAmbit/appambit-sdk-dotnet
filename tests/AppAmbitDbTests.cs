using System.Reflection;
using AppAmbit;
using AppAmbit.Attributes;
using AppAmbit.Models.Db;
using AppAmbit.Services.Interfaces;
using Moq;
using Xunit;

namespace AppAmbitTest;

[Collection("AppAmbitDb Tests")]
public class AppAmbitDbTests : IDisposable
{
    private readonly Mock<IDbService> _mockDb;

    public AppAmbitDbTests()
    {
        _mockDb = new Mock<IDbService>();
        ResetState();
        AppAmbitDb.Initialize(_mockDb.Object);
    }

    public void Dispose() => ResetState();

    private static void ResetState()
    {
        typeof(AppAmbitDb)
            .GetField("_dbService", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, null);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static DbResult OkResult(List<string>? columns = null, List<List<object?>>? rows = null,
        int rowsRead = 0, int rowsWritten = 0) =>
        new() { Columns = columns ?? new(), Rows = rows ?? new(), RowsRead = rowsRead, RowsWritten = rowsWritten };

    private static DbResult ErrorResult(string error) => new() { Error = error };

    private static List<List<object?>> MakeRows(params object?[][] rows) =>
        rows.Select(r => r.ToList()).ToList();

    // ── Group 1: Execute ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithoutParams_CallsServiceWithSql()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.Execute("SELECT 1");

        Assert.Equal("SELECT 1", capturedSql);
    }

    [Fact]
    public async Task Execute_WithParams_ForwardsParamsToService()
    {
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.Execute("SELECT * FROM tasks WHERE id = ?", 42);

        Assert.NotNull(capturedParams);
        Assert.Single(capturedParams!);
        Assert.Equal(42, capturedParams![0]);
    }

    [Fact]
    public async Task Execute_WhenServiceReturnsError_ResultHasError()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorResult("table not found"));

        var result = await AppAmbitDb.Execute("SELECT * FROM missing");

        Assert.True(result.HasError);
        Assert.Equal("table not found", result.Error);
    }

    [Fact]
    public void Execute_WhenNotInitialized_ThrowsInvalidOperationException()
    {
        ResetState();
        Assert.Throws<InvalidOperationException>(() => { _ = AppAmbitDb.Execute("SELECT 1"); });
    }

    // ── Group 2: Batch ────────────────────────────────────────────────────────

    [Fact]
    public async Task Batch_SendsAllStatementsWithTransactionFalse()
    {
        IEnumerable<DbStatement>? capturedStatements = null;
        bool? capturedTx = null;
        _mockDb.Setup(s => s.BatchAsync(It.IsAny<IEnumerable<DbStatement>>(), It.IsAny<bool>()))
            .Callback<IEnumerable<DbStatement>, bool, CancellationToken>((stmts, tx, _ct) => { capturedStatements = stmts; capturedTx = tx; })
            .ReturnsAsync(new List<DbResult> { OkResult(rowsWritten: 1), OkResult(rowsWritten: 1) });

        await AppAmbitDb.Batch(DbStatement.Of("INSERT INTO t VALUES (?)","a"), DbStatement.Of("INSERT INTO t VALUES (?)", "b"));

        Assert.NotNull(capturedStatements);
        Assert.Equal(2, capturedStatements!.Count());
        Assert.False(capturedTx);
    }

    [Fact]
    public async Task Batch_ReturnsResultPerStatement()
    {
        _mockDb.Setup(s => s.BatchAsync(It.IsAny<IEnumerable<DbStatement>>(), false))
            .ReturnsAsync(new List<DbResult> { OkResult(rowsWritten: 1), OkResult(rowsWritten: 1), OkResult() });

        var results = await AppAmbitDb.Batch(
            DbStatement.Of("INSERT INTO t(x) VALUES (?)", 1),
            DbStatement.Of("INSERT INTO t(x) VALUES (?)", 2),
            DbStatement.Of("SELECT COUNT(*) FROM t"));

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task BatchInTransaction_SendsWithTransactionTrue()
    {
        bool? capturedTx = null;
        _mockDb.Setup(s => s.BatchAsync(It.IsAny<IEnumerable<DbStatement>>(), It.IsAny<bool>()))
            .Callback<IEnumerable<DbStatement>, bool, CancellationToken>((_, tx, _ct) => capturedTx = tx)
            .ReturnsAsync(new List<DbResult> { OkResult(rowsWritten: 1) });

        await AppAmbitDb.BatchInTransaction(DbStatement.Of("INSERT INTO t VALUES (1)"));

        Assert.True(capturedTx);
    }

    [Fact]
    public async Task BatchInTransaction_WhenOneStatementErrors_ResultContainsError()
    {
        _mockDb.Setup(s => s.BatchAsync(It.IsAny<IEnumerable<DbStatement>>(), true))
            .ReturnsAsync(new List<DbResult> { OkResult(rowsWritten: 1), ErrorResult("constraint violation") });

        var results = await AppAmbitDb.BatchInTransaction(
            DbStatement.Of("INSERT INTO t VALUES (1)"),
            DbStatement.Of("INSERT INTO t VALUES (1)"));

        Assert.Contains(results, r => r.HasError);
    }

    // ── Group 3: DbQueryBuilder SELECT ──────────────────────────────────────

    [Fact]
    public async Task From_Get_GeneratesSelectStar()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Get();

        Assert.NotNull(capturedSql);
        Assert.Contains("SELECT *", capturedSql);
        Assert.Contains("FROM \"tasks\"", capturedSql);
    }

    [Fact]
    public async Task From_Select_GeneratesSpecificColumns()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Select("id", "title").Get();

        Assert.Contains("\"id\"", capturedSql);
        Assert.Contains("\"title\"", capturedSql);
        Assert.DoesNotContain("SELECT *", capturedSql);
    }

    [Fact]
    public async Task From_Where_Equality_AddsWhereClause()
    {
        string? capturedSql = null;
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, p, _ct) => { capturedSql = sql; capturedParams = p; })
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Where("is_completed", 0).Get();

        Assert.Contains("WHERE", capturedSql);
        Assert.Contains("\"is_completed\" = ?", capturedSql);
        Assert.Equal(0, capturedParams![0]);
    }

    [Fact]
    public async Task From_Where_WithOperator_AddsCorrectOperator()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Where("due_date", ">=", "2026-01-01").Get();

        Assert.Contains("\"due_date\" >= ?", capturedSql);
    }

    [Fact]
    public void From_Where_InvalidOperator_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AppAmbitDb.From("tasks").Where("col", "DROP", "val"));
    }

    [Fact]
    public async Task From_WhereIn_GeneratesInClause()
    {
        string? capturedSql = null;
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, p, _ct) => { capturedSql = sql; capturedParams = p; })
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").WhereIn("priority", new object?[] { "high", "medium" }).Get();

        Assert.Contains("IN (?, ?)", capturedSql);
        Assert.Equal("high", capturedParams![0]);
        Assert.Equal("medium", capturedParams![1]);
    }

    [Fact]
    public async Task From_OrderBy_AddsOrderByAsc()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").OrderBy("due_date").Get();

        Assert.Contains("ORDER BY \"due_date\"", capturedSql);
        Assert.DoesNotContain("DESC", capturedSql);
    }

    [Fact]
    public async Task From_OrderByDesc_AddsOrderByDesc()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").OrderByDesc("due_date").Get();

        Assert.Contains("ORDER BY \"due_date\" DESC", capturedSql);
    }

    [Fact]
    public async Task From_Limit_AddsLimitClause()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Limit(5).Get();

        Assert.Contains("LIMIT 5", capturedSql);
    }

    [Fact]
    public async Task From_Offset_AddsOffsetClause()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Limit(5).Offset(10).Get();

        Assert.Contains("LIMIT 5", capturedSql);
        Assert.Contains("OFFSET 10", capturedSql);
    }

    [Fact]
    public async Task From_First_ForcesLimit1InSql()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").First();

        Assert.Contains("LIMIT 1", capturedSql);
    }

    [Fact]
    public async Task From_First_WhenEmpty_ReturnsNull()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(columns: new() { "id" }, rows: new()));

        var result = await AppAmbitDb.From("tasks").First();

        Assert.Null(result);
    }

    [Fact]
    public async Task From_Count_GeneratesCountSql()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult(columns: new() { "COUNT(*)" }, rows: MakeRows(new object?[] { 7 })));

        await AppAmbitDb.From("tasks").Count();

        Assert.Contains("SELECT COUNT(*)", capturedSql);
        Assert.Contains("FROM \"tasks\"", capturedSql);
    }

    [Fact]
    public async Task From_Count_WithWhere_AppliesWhere()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult(columns: new() { "COUNT(*)" }, rows: MakeRows(new object?[] { 3 })));

        var count = await AppAmbitDb.From("tasks").Where("is_completed", 0).Count();

        Assert.Contains("WHERE", capturedSql);
        Assert.Equal(3, count);
    }

    // ── Group 4: DbQueryBuilder mutations ────────────────────────────────────

    [Fact]
    public async Task From_Insert_GeneratesInsertSql()
    {
        string? capturedSql = null;
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, p, _ct) => { capturedSql = sql; capturedParams = p; })
            .ReturnsAsync(OkResult(rowsWritten: 1));

        await AppAmbitDb.From("tasks").Insert(new Dictionary<string, object?> { { "title", "Test" }, { "priority", "high" } });

        Assert.Contains("INSERT INTO \"tasks\"", capturedSql);
        Assert.Contains("\"title\"", capturedSql);
        Assert.Contains("\"priority\"", capturedSql);
        Assert.Contains("VALUES (?, ?)", capturedSql);
        Assert.Equal("Test", capturedParams![0]);
        Assert.Equal("high", capturedParams![1]);
    }

    [Fact]
    public async Task From_Update_WithWhere_GeneratesUpdateSql()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult(rowsWritten: 1));

        await AppAmbitDb.From("tasks")
            .Where("title", "Test")
            .Update(new Dictionary<string, object?> { { "is_completed", 1 } });

        Assert.Contains("UPDATE \"tasks\"", capturedSql);
        Assert.Contains("SET \"is_completed\" = ?", capturedSql);
        Assert.Contains("WHERE", capturedSql);
    }

    [Fact]
    public async Task From_Update_WithoutWhere_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppAmbitDb.From("tasks").Update(new Dictionary<string, object?> { { "is_completed", 1 } }));
    }

    [Fact]
    public async Task From_Delete_WithWhere_GeneratesDeleteSql()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult(rowsWritten: 2));

        await AppAmbitDb.From("tasks").Where("is_completed", 1).Delete();

        Assert.Contains("DELETE FROM \"tasks\"", capturedSql);
        Assert.Contains("WHERE", capturedSql);
    }

    [Fact]
    public async Task From_Delete_WithoutWhere_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppAmbitDb.From("tasks").Delete());
    }

    // ── Group 5: Typed model mapping ──────────────────────────────────────────

    [Fact]
    public void MapTo_MapsColumnsByPropertyName()
    {
        var result = OkResult(
            columns: new() { "Id", "Title" },
            rows: MakeRows(new object?[] { 1, "Buy milk" }));

        var items = result.MapTo<SimpleModel>();

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Buy milk", items[0].Title);
    }

    [Fact]
    public void MapTo_MapsColumnsByDbColumnAttribute()
    {
        var result = OkResult(
            columns: new() { "is_completed", "due_date" },
            rows: MakeRows(new object?[] { 1, "2026-06-10" }));

        var items = result.MapTo<AnnotatedModel>();

        Assert.Single(items);
        Assert.Equal(1, items[0].IsCompleted);
        Assert.Equal("2026-06-10", items[0].DueDate);
    }

    [Fact]
    public void MapTo_IgnoresUnknownColumns()
    {
        var result = OkResult(
            columns: new() { "Id", "unknown_column" },
            rows: MakeRows(new object?[] { 5, "ignored" }));

        var items = result.MapTo<SimpleModel>();

        Assert.Single(items);
        Assert.Equal(5, items[0].Id);
    }

    [Fact]
    public void MapTo_HandlesNullValues()
    {
        var result = OkResult(
            columns: new() { "Id", "Title" },
            rows: MakeRows(new object?[] { 3, null }));

        var items = result.MapTo<SimpleModel>();

        Assert.Single(items);
        Assert.Equal(3, items[0].Id);
        Assert.Null(items[0].Title);
    }

    [Fact]
    public void MapTo_CastsIntCorrectly()
    {
        var result = OkResult(
            columns: new() { "Id" },
            rows: MakeRows(new object?[] { 42L })); // long from JSON

        var items = result.MapTo<SimpleModel>();

        Assert.Equal(42, items[0].Id);
    }

    // ── Group 6: BUG-01 — Offset(0) ──────────────────────────────────────────

    [Fact]
    public async Task From_Offset_Zero_EmitsOffsetClause()
    {
        // BUG-01: guard was > 0, silently dropped OFFSET 0
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Limit(5).Offset(0).Get();

        Assert.Contains("LIMIT 5", capturedSql);
        Assert.Contains("OFFSET 0", capturedSql);
    }

    [Fact]
    public async Task From_Offset_Zero_OnFirst_EmitsOffsetClause()
    {
        // Offset(0) must also work when combined with First()
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult(columns: new() { "id" }, rows: new()));

        await AppAmbitDb.From("tasks").Offset(0).First();

        Assert.Contains("OFFSET 0", capturedSql);
    }

    // ── Group 7: BUG-03 — bool mapping from integer ───────────────────────────

    [Fact]
    public void MapTo_Bool_FromInteger1_MapsToTrue()
    {
        // BUG-03: bool.Parse("1") throws FormatException — SQLite stores bool as 0/1
        var result = OkResult(
            columns: new() { "IsActive" },
            rows: MakeRows(new object?[] { 1L }));

        var items = result.MapTo<BoolModel>();

        Assert.True(items[0].IsActive);
    }

    [Fact]
    public void MapTo_Bool_FromInteger0_MapsToFalse()
    {
        var result = OkResult(
            columns: new() { "IsActive" },
            rows: MakeRows(new object?[] { 0L }));

        var items = result.MapTo<BoolModel>();

        Assert.False(items[0].IsActive);
    }

    [Fact]
    public void MapTo_Bool_FromBoolTrue_MapsCorrectly()
    {
        var result = OkResult(
            columns: new() { "IsActive" },
            rows: MakeRows(new object?[] { true }));

        var items = result.MapTo<BoolModel>();

        Assert.True(items[0].IsActive);
    }

    // ── Group 8: BUG-02 — builder consumed guard ─────────────────────────────

    [Fact]
    public async Task Builder_ExecutedTwice_ThrowsInvalidOperationException()
    {
        // BUG-02: reusing builder after execution must be rejected
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult());

        var builder = AppAmbitDb.From("tasks");
        await builder.Get();

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.Get());
    }

    [Fact]
    public async Task Builder_FirstThenGet_ThrowsInvalidOperationException()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(columns: new() { "id" }, rows: new()));

        var builder = AppAmbitDb.From("tasks");
        await builder.First();

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.Get());
    }

    [Fact]
    public async Task Builder_CountThenDelete_ThrowsInvalidOperationException()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(columns: new() { "COUNT(*)" }, rows: MakeRows(new object?[] { 0 })));

        var builder = AppAmbitDb.From("tasks").Where("is_completed", 1);
        await builder.Count();

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.Delete());
    }

    // ── Group 9: BUG-04 — API contract consistency ────────────────────────────
    // Get/First/Count now return empty/null/0 on error (aligned with Execute() HasError pattern)

    [Fact]
    public async Task From_Get_WhenServiceReturnsError_ReturnsEmptyList()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorResult("table not found"));

        var result = await AppAmbitDb.From("tasks").Get();

        Assert.Empty(result);
    }

    [Fact]
    public async Task From_First_WhenServiceReturnsError_ReturnsNull()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorResult("connection timeout"));

        var result = await AppAmbitDb.From("tasks").First();

        Assert.Null(result);
    }

    [Fact]
    public async Task From_Count_WhenServiceReturnsError_ReturnsZero()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorResult("permission denied"));

        var count = await AppAmbitDb.From("tasks").Where("x", 1).Count();

        Assert.Equal(0, count);
    }

    // ── Group 10: CODE-02 — QuoteId injection guard ───────────────────────────

    [Fact]
    public void From_TableName_WithDoubleQuote_IsEscapedCorrectly()
    {
        // QuoteId must escape " → "" (SQLite identifier escaping)
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        // Column with embedded quote — must not break SQL
        _ = AppAmbitDb.From("tasks").Where("col\"name", 1).Get();

        Assert.Contains("\"col\"\"name\"", capturedSql);
    }

    [Fact]
    public void From_TableName_WithControlCharacter_ThrowsArgumentException()
    {
        // CODE-02: null byte or control char in identifier must be rejected
        Assert.Throws<ArgumentException>(() =>
            AppAmbitDb.From("tasks").Where("col\0name", 1));
    }

    [Fact]
    public void From_EmptyColumnName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AppAmbitDb.From("tasks").Where("", 1));
    }

    // ── Group 11: CODE-04 — Count() zero/null rows ────────────────────────────

    [Fact]
    public async Task Count_WhenRowsEmpty_ReturnsZero()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(columns: new() { "COUNT(*)" }, rows: new()));

        var count = await AppAmbitDb.From("tasks").Count();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Count_WhenValueIsLong_CastsToInt()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(
                columns: new() { "COUNT(*)" },
                rows: MakeRows(new object?[] { 99L })));

        var count = await AppAmbitDb.From("tasks").Count();

        Assert.Equal(99, count);
    }

    // ── Group 12: multiple Where() conditions ────────────────────────────────

    [Fact]
    public async Task From_MultipleWhere_JoinsWithAnd()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("is_completed", 0)
            .Where("priority", "high")
            .Get();

        Assert.Contains("WHERE", capturedSql);
        Assert.Contains("AND", capturedSql);
        Assert.Contains("\"is_completed\" = ?", capturedSql);
        Assert.Contains("\"priority\" = ?", capturedSql);
    }

    [Fact]
    public async Task From_MultipleWhere_ForwardsAllParams()
    {
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("is_completed", 0)
            .Where("priority", "high")
            .Get();

        Assert.Equal(2, capturedParams!.Count);
        Assert.Equal(0, capturedParams[0]);
        Assert.Equal("high", capturedParams[1]);
    }

    // ── Group 13: Insert edge cases ───────────────────────────────────────────

    [Fact]
    public async Task From_Insert_WithNullValue_IncludesNullParam()
    {
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult(rowsWritten: 1));

        await AppAmbitDb.From("tasks").Insert(new Dictionary<string, object?>
        {
            { "title", "Test" },
            { "due_date", null }
        });

        Assert.Equal(2, capturedParams!.Count);
        Assert.Null(capturedParams[1]);
    }

    [Fact]
    public async Task From_Update_ParamsOrderIsSetClauseThenWhere()
    {
        // UPDATE SET params must come before WHERE params
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult(rowsWritten: 1));

        await AppAmbitDb.From("tasks")
            .Where("id", 42)
            .Update(new Dictionary<string, object?> { { "is_completed", 1 } });

        // params: [SET value=1, WHERE id=42]
        Assert.Equal(2, capturedParams!.Count);
        Assert.Equal(1, capturedParams[0]); // SET value
        Assert.Equal(42, capturedParams[1]); // WHERE value
    }

    // ── Group 14: WhereIn edge cases ─────────────────────────────────────────

    [Fact]
    public async Task From_WhereIn_SingleValue_GeneratesInWithOnePlaceholder()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").WhereIn("priority", new object?[] { "high" }).Get();

        Assert.Contains("IN (?)", capturedSql);
    }

    [Fact]
    public async Task From_WhereIn_WithNullValue_IncludesNullParam()
    {
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").WhereIn("priority", new object?[] { "high", null }).Get();

        Assert.Equal(2, capturedParams!.Count);
        Assert.Null(capturedParams[1]);
    }

    // ── Group 15: CODE-06 — OrWhere and WhereGroup ───────────────────────────

    [Fact]
    public async Task From_OrWhere_GeneratesOrInSql()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("priority", "high")
            .OrWhere("priority", "medium")
            .Get();

        Assert.Contains("OR", capturedSql);
        Assert.Contains("\"priority\" = ?", capturedSql);
    }

    [Fact]
    public async Task From_OrWhere_ForwardsAllParams()
    {
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("priority", "high")
            .OrWhere("priority", "medium")
            .Get();

        Assert.Equal(2, capturedParams!.Count);
        Assert.Equal("high", capturedParams[0]);
        Assert.Equal("medium", capturedParams[1]);
    }

    [Fact]
    public async Task From_WhereGroup_GeneratesParenthesizedGroup()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("is_completed", 0)
            .WhereGroup(g => g.Where("priority", "high").Where("due_date", "2026-01-01"))
            .Get();

        Assert.Contains("WHERE", capturedSql);
        Assert.Contains("(", capturedSql);
        Assert.Contains(")", capturedSql);
        Assert.Contains("AND", capturedSql);
    }

    [Fact]
    public void OrWhere_InvalidOperator_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AppAmbitDb.From("tasks").OrWhere("col", "DROP", "val"));
    }

    // ── Group 16: CODE-05 — CancellationToken propagation ────────────────────

    [Fact]
    public async Task Execute_WithCancellationToken_PropagatesToken()
    {
        CancellationToken? capturedToken = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(OkResult());

        using var cts = new CancellationTokenSource();
        await AppAmbitDb.Execute("SELECT 1", cts.Token);

        Assert.NotNull(capturedToken);
    }

    [Fact]
    public async Task From_Get_WithCancelledToken_DoesNotCallService()
    {
        // CancellationToken passed to QueryAsync — service should receive it
        CancellationToken? capturedToken = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(OkResult());

        using var cts = new CancellationTokenSource();
        await AppAmbitDb.From("tasks").Get(cts.Token);

        Assert.NotNull(capturedToken);
        Assert.Equal(cts.Token, capturedToken);
    }

    // ── Group 17: BUG-05 — AppAmbitDb.Initialize re-init guard ───────────────

    [Fact]
    public void Initialize_SameInstance_DoesNotWarn()
    {
        // Re-initializing with same instance: no-op, no warning
        var mock = new Mock<IDbService>();
        AppAmbitDb.Initialize(mock.Object);
        AppAmbitDb.Initialize(mock.Object); // same instance — should be fine
    }

    [Fact]
    public void Initialize_DifferentInstance_OverwritesSilently()
    {
        // Re-initializing with different instance: overwrites (warning via Debug.WriteLine, not throw)
        var mock1 = new Mock<IDbService>();
        var mock2 = new Mock<IDbService>();
        AppAmbitDb.Initialize(mock1.Object);
        AppAmbitDb.Initialize(mock2.Object); // different instance — logs warning, doesn't throw
    }

    // ── Group 18: CODE-04 — Count unexpected type throws ─────────────────────

    [Fact]
    public async Task Count_WhenValueIsUnexpectedType_Throws()
    {
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkResult(
                columns: new() { "COUNT(*)" },
                rows: MakeRows(new object?[] { new object() }))); // non-IConvertible value

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppAmbitDb.From("tasks").Count());
    }

    // ── Group 19: BUG — Limit(0) silently ignored ────────────────────────────
    // DbQueryBuilder.cs line 212: `if (effectiveLimit > 0)` drops LIMIT 0 from SQL.
    // This test documents the bug: Limit(0) currently emits no LIMIT clause,
    // so the backend returns all rows instead of zero.

    [Fact]
    public async Task From_Limit_Zero_EmitsLimitClause()
    {
        // BUG: guard is `> 0`, so LIMIT 0 is silently dropped — this test FAILS today.
        // Fix: change guard to `>= 0` (mirroring the already-fixed Offset(0) guard).
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Limit(0).Get();

        Assert.Contains("LIMIT 0", capturedSql);
    }

    [Fact]
    public async Task From_Limit_Zero_SqlEndsWithLimitClause()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Limit(0).Get();

        Assert.Contains("LIMIT 0", capturedSql);
        Assert.NotEqual("SELECT * FROM \"tasks\"", capturedSql);
    }

    // ── Group 20: Where(column, null) auto-rewrites to IS NULL ──────────────
    // Standard SQL: `x = NULL` → UNKNOWN → zero rows. Builder auto-rewrites to IS NULL.

    [Fact]
    public async Task Where_NullValue_GeneratesIsNull()
    {
        string? capturedSql = null;
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, p, _ct) => { capturedSql = sql; capturedParams = p; })
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks").Where("deleted_at", null).Get();

        Assert.Contains("\"deleted_at\" IS NULL", capturedSql);
        Assert.DoesNotContain("= ?", capturedSql);
        Assert.Empty(capturedParams!);
    }

    [Fact]
    public async Task OrWhere_NullValue_GeneratesOrIsNull()
    {
        string? capturedSql = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((sql, _, _ct) => capturedSql = sql)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("priority", "high")
            .OrWhere("deleted_at", null)
            .Get();

        Assert.Contains("\"deleted_at\" IS NULL", capturedSql);
    }

    [Fact]
    public async Task Where_NullValue_MixedWithNonNull_CorrectParamCount()
    {
        List<object?>? capturedParams = null;
        _mockDb.Setup(s => s.QueryAsync(It.IsAny<string>(), It.IsAny<List<object?>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<object?>?, CancellationToken>((_, p, _ct) => capturedParams = p)
            .ReturnsAsync(OkResult());

        await AppAmbitDb.From("tasks")
            .Where("is_completed", 0)
            .Where("deleted_at", null)
            .Get();

        // Only one param bound: the non-null Where("is_completed", 0)
        Assert.Single(capturedParams!);
        Assert.Equal(0, capturedParams![0]);
    }

    // ── inner test models ─────────────────────────────────────────────────────

    private class SimpleModel
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    private class AnnotatedModel
    {
        [DbColumn("is_completed")]
        public int IsCompleted { get; set; }

        [DbColumn("due_date")]
        public string? DueDate { get; set; }
    }

    private class BoolModel
    {
        public bool IsActive { get; set; }
    }
}
