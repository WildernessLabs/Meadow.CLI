namespace Meadow.Cloud.Client.Devices;

public class AddDeviceRequest(string id, string orgId, string publicKey)
{
    public AddDeviceRequest(string id, string name, string orgId, string publicKey)
        : this(id, orgId, publicKey)
    {
        Name = name;
    }

    public AddDeviceRequest(string id, string name, string orgId, string collectionId, string publicKey)
        : this(id, orgId, publicKey)
    {
        Name = name;
        CollectionId = collectionId;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } = id;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = orgId;

    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; set; }

    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; } = publicKey;
}
