using AppAmbit.Models.Db;
using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints;

internal sealed class DbBatchEndpoint : BaseEndpoint
{
    public DbBatchEndpoint(IEnumerable<DbStatement> statements, bool inTransaction)
    {
        Url = "/db/batch";
        Method = HttpMethodEnum.Post;
        Payload = new DbBatchRequest(statements, inTransaction);
    }
}
