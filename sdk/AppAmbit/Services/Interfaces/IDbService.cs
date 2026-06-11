using AppAmbit.Models.Db;

namespace AppAmbit.Services.Interfaces;

public interface IDbService
{
    Task<DbResult> QueryAsync(string sql, List<object?>? parameters = null, CancellationToken ct = default);
    Task<List<DbResult>> BatchAsync(IEnumerable<DbStatement> statements, bool inTransaction, CancellationToken ct = default);
}
