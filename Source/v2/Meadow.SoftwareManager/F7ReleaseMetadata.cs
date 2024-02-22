using System.Text.Json.Serialization;

namespace Meadow.Software;

public class F7ReleaseMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
    [JsonPropertyName("minCLIVersion")]
    public string MinCLIVersion { get; set; } = default!;
    [JsonPropertyName("downloadUrl")]
    public string DownloadURL { get; set; } = default!;
    [JsonPropertyName("networkDownloadUrl")]
    public string NetworkDownloadURL { get; set; } = default!;

}
