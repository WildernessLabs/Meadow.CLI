using System.Text.Json.Serialization;

namespace Meadow.Cloud.Client;

public record Collection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;
    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = default!;
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
    [JsonPropertyName("lastPublishedPackageId")]
    public string LastPublishedPackageId { get; set; } = default!;
    [JsonPropertyName("lastPublishedDate")]
    public DateTime? LastPublishedDate { get; set; } = default!;
}
