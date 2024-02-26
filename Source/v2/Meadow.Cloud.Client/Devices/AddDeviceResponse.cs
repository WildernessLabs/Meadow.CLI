namespace Meadow.Cloud.Client.Devices;

public class AddDeviceResponse(string id, string name, string orgId, string collectionId)
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = id;

    [JsonPropertyName("name")]
    public string? Name { get; set; } = name;

    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = orgId;

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; set; } = collectionId;
}