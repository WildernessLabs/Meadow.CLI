namespace Meadow.Cloud.Client;

public record PackageInfo
{
    [JsonPropertyName("v")]
    public string Version { get; set; }
    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; }
}
