namespace Meadow.Cloud.Client.Firmware;

public class GetFirmwareVersionsResponse(string version, DateTimeOffset lastModifiedAt)
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = version;

    [JsonPropertyName("lastModifiedAt")]
    public DateTimeOffset LastModifiedAt { get; set; } = lastModifiedAt;
}
