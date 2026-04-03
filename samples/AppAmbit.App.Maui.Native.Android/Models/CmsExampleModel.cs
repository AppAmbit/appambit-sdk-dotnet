using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AppAmbitTestingAppAndroid.Models;

public class CmsExampleModel
{
    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("category")]
    public List<string>? Category { get; set; }

    [JsonProperty("in_stock")]
    public bool InStock { get; set; }

    [JsonProperty("item_sku")]
    public string? ItemSku { get; set; }

    [JsonProperty("entry_date")]
    public string? EntryDate { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("product_name")]
    public string? ProductName { get; set; }

    [JsonProperty("product_image")]
    public string? ProductImage { get; set; }

    [JsonProperty("product_image_url")]
    public string? ProductImageUrl { get; set; }

    [JsonProperty("support_email")]
    public string? SupportEmail { get; set; }

    [JsonProperty("technical_specs")]
    public Dictionary<string, string>? TechnicalSpecs { get; set; }

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("published_at")]
    public DateTimeOffset PublishedAt { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
