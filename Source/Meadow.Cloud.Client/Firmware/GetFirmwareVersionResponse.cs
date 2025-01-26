namespace Meadow.Cloud.Client.Firmware;

public class GetFirmwareVersionResponse
{
    public GetFirmwareVersionResponse(string version, string minCLIVersion, string downloadUrl, string networkDownloadUrl)
    {
        Version = version;
        MinCLIVersion = minCLIVersion;
        DownloadUrl = downloadUrl;
        NetworkDownloadUrl = networkDownloadUrl;
    }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("minCLIVersion")]
    public string MinCLIVersion { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }

    [JsonPropertyName("networkDownloadUrl")]
    public string NetworkDownloadUrl { get; set; }
}
