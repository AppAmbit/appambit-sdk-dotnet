using Newtonsoft.Json;

namespace AppAmbit.Models.Db;

internal sealed class DbBatchRequest
{
    [JsonProperty("statements")]
    public List<DbStatementPayload> Statements { get; }

    [JsonProperty("transaction")]
    public bool Transaction { get; }

    public DbBatchRequest(IEnumerable<DbStatement> statements, bool inTransaction)
    {
        Statements = statements.Select(s => new DbStatementPayload(s)).ToList();
        Transaction = inTransaction;
    }
}

internal sealed class DbStatementPayload
{
    [JsonProperty("sql")]
    public string Sql { get; }

    [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
    public List<object?>? Params { get; }

    public DbStatementPayload(DbStatement s)
    {
        Sql = s.Sql;
        Params = s.Params;
    }
}
