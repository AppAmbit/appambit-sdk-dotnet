namespace AppAmbit.Services.Interfaces;

internal interface IHttpTransport
{
    Task<HttpResponseSnapshot> ExecuteRawRequestAsync(
        IEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
