using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using SQLite;

namespace AppAmbit.Models.RemoteConfigs;

public class RemoteConfig
{
    [JsonProperty("key")]
    public string? Key { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }
    
}