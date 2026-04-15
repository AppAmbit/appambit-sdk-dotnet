using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints;

internal class CmsEndpoint : BaseEndpoint
{
    public override string BaseUrl => AppConstants.CmsBaseUrl;

    public CmsEndpoint(string slug, int page, int perPage)
    {
        this.Method = HttpMethodEnum.Get;
        this.Url = "/" + slug + $"/?per_page={perPage}&page={page}";
    }
}
