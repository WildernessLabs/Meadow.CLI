using System.Text.Json.Serialization;

namespace Meadow.Package;

public record PackageInfo
{
    [JsonPropertyName("v")]
    public string Version { get; set; }
    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; }
}
