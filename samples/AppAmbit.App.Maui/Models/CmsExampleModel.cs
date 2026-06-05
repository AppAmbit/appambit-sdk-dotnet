using Newtonsoft.Json;
using System.Collections.Generic;

namespace AppAmbitTestingApp.Models;

public class CmsExampleModel
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("body")]
    public string? Body { get; set; }

    [JsonProperty("category")]
    public List<string>? Category { get; set; }

    [JsonProperty("author_email")]
    public string? AuthorEmail { get; set; }

    [JsonProperty("featured_image")]
    public string? FeaturedImageUrl { get; set; }

    [JsonProperty("views_count")]
    public double? ViewsCount { get; set; }

    [JsonProperty("is_published")]
    public bool? IsPublished { get; set; }

    [JsonProperty("event_date")]
    public string? EventDate { get; set; }

    [JsonProperty("published_at")]
    public string? PublishedAt { get; set; }
}
