namespace Meadow.Cloud.Client;

public record Package
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;
    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = default!;
    [JsonPropertyName("publishedDate")]
    public DateTime? PublishedDate { get; set; } = default!;
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
    [JsonPropertyName("description")]
    public string Description { get; set; } = default!;
}
