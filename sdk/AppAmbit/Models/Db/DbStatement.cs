namespace AppAmbit.Models.Db;

public sealed class DbStatement
{
    public string Sql { get; }
    public List<object?>? Params { get; }

    private DbStatement(string sql, List<object?>? parameters)
    {
        Sql = sql;
        Params = parameters is { Count: > 0 } ? parameters : null;
    }

    public static DbStatement Of(string sql) => new(sql, null);

    public static DbStatement Of(string sql, params object?[] parameters) =>
        new(sql, parameters.Length > 0 ? new List<object?>(parameters) : null);
}
