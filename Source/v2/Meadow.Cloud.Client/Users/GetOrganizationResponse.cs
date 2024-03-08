namespace Meadow.Cloud.Client.Users;

public class GetOrganizationResponse
{
    public GetOrganizationResponse(string id, string name, string defaultCollectionId)
    {
        Id = id;
        Name = name;
        DefaultCollectionId = defaultCollectionId;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("defaultCollectionId")]
    public string DefaultCollectionId { get; set; }
}
