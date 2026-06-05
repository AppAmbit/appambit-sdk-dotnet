using AppAmbit.Models.Db;
using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints;

internal sealed class DbQueryEndpoint : BaseEndpoint
{
    public DbQueryEndpoint(string sql, List<object?>? parameters)
    {
        Url = "/db/query";
        Method = HttpMethodEnum.Post;
        Payload = new DbQueryRequest(sql, parameters);
    }
}
