using Newtonsoft.Json;

namespace AppAmbit.Models.Db;

internal sealed class DbApiResponse
{
    [JsonProperty("results")]
    public List<DbResultDto>? Results { get; set; }

    [JsonProperty("request_id")]
    public string? RequestId { get; set; }
}

internal sealed class DbResultDto
{
    [JsonProperty("columns")]
    public List<string>? Columns { get; set; }

    [JsonProperty("rows")]
    public List<List<object?>>? Rows { get; set; }

    [JsonProperty("rows_read")]
    public int RowsRead { get; set; }

    [JsonProperty("rows_written")]
    public int RowsWritten { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    public DbResult ToDbResult()
    {
        return new DbResult
        {
            Columns = Columns ?? new List<string>(),
            Rows = Rows ?? new List<List<object?>>(),
            RowsRead = RowsRead,
            RowsWritten = RowsWritten,
            Error = Error
        };
    }
}
