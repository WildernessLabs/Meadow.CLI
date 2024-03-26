namespace Meadow.Cloud.Client.Firmware;

public class GetFirmwareVersionsResponse
{
    public GetFirmwareVersionsResponse(string version, DateTimeOffset lastModifiedAt)
    {
        Version = version;
        LastModifiedAt = lastModifiedAt;
    }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("lastModifiedAt")]
    public DateTimeOffset LastModifiedAt { get; set; }
}
