using Newtonsoft.Json;

namespace AppAmbit.Models.Cms;

public class CmsResponse<T>
{
    [JsonProperty("data")]
    public T? Data { get; set; }

    [JsonProperty("meta")]
    public CmsMeta? Meta { get; set; }
}

public class CmsMeta
{
    [JsonProperty("total")]
    public int Total { get; set; }

    [JsonProperty("per_page")]
    public int PerPage { get; set; }

    [JsonProperty("current_page")]
    public int CurrentPage { get; set; }

    [JsonProperty("last_page")]
    public int LastPage { get; set; }
}
