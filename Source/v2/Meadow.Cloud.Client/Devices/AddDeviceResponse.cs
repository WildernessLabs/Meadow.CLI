using System.Text.Json.Serialization;

namespace Meadow.Cloud.Client.Devices;

public class AddDeviceResponse
{
    public AddDeviceResponse(string id, string name, string orgId, string collectionId)
    {
        Id = id;
        Name = name;
        OrgId = orgId;
        CollectionId = collectionId;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("orgId")]
    public string OrgId { get; set; }

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; set; }
}