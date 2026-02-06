using Newtonsoft.Json;
using System.Collections.Generic;

namespace AppAmbit.Models.Responses;

public class RemoteConfigResponse
{
    [JsonProperty("configs")]
    public Dictionary<string, object> Configs { get; set; }
}