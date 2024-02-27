namespace Meadow.Cloud.Client.Users;

public class GetOrganizationResponse(string id, string name, string defaultCollectionId)
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = id;

    [JsonPropertyName("name")]
    public string Name { get; set; } = name;

    [JsonPropertyName("defaultCollectionId")]
    public string DefaultCollectionId { get; set; } = defaultCollectionId;
}
