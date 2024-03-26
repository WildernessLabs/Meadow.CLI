namespace Meadow.Cloud.Client;

public class UserOrg
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("defaultCollectionId")]
    public string DefaultCollectionId { get; set; }
}