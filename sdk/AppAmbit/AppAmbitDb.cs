using System.Diagnostics;
using AppAmbit.Models.Db;
using AppAmbit.Services.Interfaces;

namespace AppAmbit;

public static class AppAmbitDb
{
    private static IDbService? _dbService;

    internal static void Initialize(IDbService dbService)
    {
        if (_dbService != null && _dbService != dbService)
            Debug.WriteLine("[AppAmbitDb] Warning: Re-initializing DbService. Previous instance discarded.");
        _dbService = dbService;
    }

    private static IDbService EnsureInitialized() =>
        _dbService ?? throw new InvalidOperationException(
            "AppAmbit SDK is not initialized. Call AppAmbit.start() first.");

    /// <summary>Execute raw SQL with no parameters.</summary>
    public static Task<DbResult> Execute(string sql, CancellationToken ct = default) =>
        EnsureInitialized().QueryAsync(sql, null, ct);

    /// <summary>Execute raw SQL with positional ? parameters.</summary>
    public static Task<DbResult> Execute(string sql, params object?[] parameters) =>
        EnsureInitialized().QueryAsync(sql, parameters.ToList());

    /// <summary>Execute raw SQL with positional ? parameters and a cancellation token.</summary>
    public static Task<DbResult> Execute(string sql, CancellationToken ct, params object?[] parameters) =>
        EnsureInitialized().QueryAsync(sql, parameters.ToList(), ct);

    /// <summary>Execute multiple statements in a single request (no transaction).</summary>
    public static Task<List<DbResult>> Batch(params DbStatement[] statements) =>
        EnsureInitialized().BatchAsync(statements, inTransaction: false);

    /// <summary>Execute multiple statements wrapped in a transaction. Rolls back on any error.</summary>
    public static Task<List<DbResult>> BatchInTransaction(params DbStatement[] statements) =>
        EnsureInitialized().BatchAsync(statements, inTransaction: true);

    /// <summary>Execute multiple statements with cancellation support.</summary>
    public static Task<List<DbResult>> Batch(CancellationToken ct, params DbStatement[] statements) =>
        EnsureInitialized().BatchAsync(statements, inTransaction: false, ct);

    /// <summary>Execute multiple statements in a transaction with cancellation support.</summary>
    public static Task<List<DbResult>> BatchInTransaction(CancellationToken ct, params DbStatement[] statements) =>
        EnsureInitialized().BatchAsync(statements, inTransaction: true, ct);

    /// <summary>Fluent query builder returning results as Dictionary&lt;string, object?&gt;.</summary>
    public static DbQueryBuilder<Dictionary<string, object?>> From(string table) =>
        new(table, EnsureInitialized());

    /// <summary>Fluent query builder mapping results to a strongly-typed model.</summary>
    public static DbQueryBuilder<T> From<T>(string table) where T : new() =>
        new(table, EnsureInitialized());
}
