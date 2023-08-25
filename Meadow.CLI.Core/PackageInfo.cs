using System.Text.Json.Serialization;

namespace Meadow.CLI.Core;

public class PackageInfo
{
    [JsonPropertyName("v")] 
    public string? Version { get; set; }
    [JsonPropertyName("osVersion")] 
    public string? OsVersion { get; set; }
}