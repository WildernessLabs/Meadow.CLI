using System.Text.Json.Serialization;

namespace Meadow.Hcom;

public class ReleaseMetadata
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    [JsonPropertyName("minCLIVersion")]
    public string? MinCLIVersion { get; set; }
    [JsonPropertyName("downloadUrl")]
    public string? DownloadURL { get; set; }
    [JsonPropertyName("networkDownloadUrl")]
    public string? NetworkDownloadURL { get; set; }

}
