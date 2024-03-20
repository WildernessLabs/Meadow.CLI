using System.Text.Json.Serialization;

namespace Meadow.Cloud.Client.Devices;

public class AddDeviceRequest
{
    public AddDeviceRequest(string id, string orgId, string publicKey)
    {
        Id = id;
        OrgId = orgId;
        PublicKey = publicKey;
    }

    public AddDeviceRequest(string id, string name, string orgId, string publicKey)
        : this(id, orgId, publicKey)
    {
        Name = name;
    }

    public AddDeviceRequest(string id, string name, string orgId, string collectionId, string publicKey)
        : this(id, name, orgId, publicKey)
    {
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

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; }
}
