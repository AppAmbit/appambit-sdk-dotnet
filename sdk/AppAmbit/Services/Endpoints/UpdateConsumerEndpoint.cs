using AppAmbit.Models.App;
using AppAmbit.Services.Endpoints.Base;
using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints;

internal class UpdateConsumerEndpoint : BaseEndpoint, IEndpoint
{
    public UpdateConsumerEndpoint(string consumerId, UpdateConsumer request)
    {
        Url = $"/consumer/{consumerId}";
        Method = HttpMethodEnum.Put;
        Payload = request;
    }
}
