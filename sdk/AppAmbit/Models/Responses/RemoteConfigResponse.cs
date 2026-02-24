using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace AppAmbit.Models.Responses;

public class RemoteConfigResponse
{
    [JsonProperty("configs")]
    [JsonConverter(typeof(ConfigsDictionaryConverter))]
    public Dictionary<string, object> Configs { get; set; }
}

internal class ConfigsDictionaryConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> ReadJson(JsonReader reader, Type objectType,
        Dictionary<string, object> existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        if (token.Type == JTokenType.Object)
        {
            return token.ToObject<Dictionary<string, object>>(serializer);
        }

        // When API returns an empty array [] instead of an object {}, return empty dictionary
        return new Dictionary<string, object>();
    }

    public override void WriteJson(JsonWriter writer, Dictionary<string, object> value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}