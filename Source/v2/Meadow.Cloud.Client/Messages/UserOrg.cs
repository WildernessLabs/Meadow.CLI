using System.Text.Json.Serialization;

namespace Meadow.Cloud;

public class UserOrg
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public object Name { get; set; }
    [JsonPropertyName("defaultCollectionId")]
    public string DefaultCollectionId { get; set; }
}
