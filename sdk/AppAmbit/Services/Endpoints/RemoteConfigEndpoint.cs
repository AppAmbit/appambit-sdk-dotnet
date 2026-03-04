using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints;

internal class RemoteConfigEndpoint : BaseEndpoint, IEndpoint
{
    public RemoteConfigEndpoint(String appVersion)
    {
        Url = $"/sdk/config?appVersion={appVersion}";
        Method = HttpMethodEnum.Get;
    }
}