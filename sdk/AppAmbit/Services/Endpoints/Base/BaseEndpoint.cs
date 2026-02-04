using AppAmbit.Services.Interfaces;

namespace AppAmbit.Services.Endpoints.Base;

internal class BaseEndpoint : IEndpoint
{
    public string Url { get; set; }
    
    public string BaseUrl { get; set; } = "https://staging-appambit.com/api";
    
    public bool SkipAuthorization { get; set;  } = false;
    
    public object Payload { get; set; } = null;
    
    public Dictionary<string, string> CustomHeader { get; set; } = null;
    
    public HttpMethodEnum Method { get; set; }
}