using Newtonsoft.Json;
using SQLite;

namespace AppAmbit.Models.RemoteConfigs;

public class RemoteConfigEntity : RemoteConfig
{
    [PrimaryKey]
    [JsonIgnore]
    public Guid Id { get; set; }
}
