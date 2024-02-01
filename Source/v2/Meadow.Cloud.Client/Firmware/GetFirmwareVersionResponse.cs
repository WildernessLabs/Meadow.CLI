namespace Meadow.Cloud.Client.Firmware;

public class GetFirmwareVersionResponse(string version, string minCLIVersion, string downloadUrl, string networkDownloadUrl)
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = version;

    [JsonPropertyName("minCLIVersion")]
    public string MinCLIVersion { get; set; } = minCLIVersion;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = downloadUrl;

    [JsonPropertyName("networkDownloadUrl")]
    public string NetworkDownloadUrl { get; set; } = networkDownloadUrl;
}
