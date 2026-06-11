using Newtonsoft.Json;

namespace AppAmbit.Models.Db;

internal sealed class DbQueryRequest
{
    [JsonProperty("sql")]
    public string Sql { get; }

    [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
    public List<object?>? Params { get; }

    public DbQueryRequest(string sql, List<object?>? parameters)
    {
        Sql = sql;
        Params = parameters is { Count: > 0 } ? parameters : null;
    }
}
