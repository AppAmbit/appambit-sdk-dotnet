using Newtonsoft.Json;

namespace AppAmbit.Models.App;

internal class UpdateConsumer
{
    public UpdateConsumer(string? deviceToken, bool? pushEnabled)
    {
        DeviceToken = deviceToken;
        PushEnabled = pushEnabled;
    }

    [JsonProperty("device_token", NullValueHandling = NullValueHandling.Ignore)]
    public string? DeviceToken { get; }

    [JsonProperty("push_enabled", NullValueHandling = NullValueHandling.Ignore)]
    public bool? PushEnabled { get; }
}
