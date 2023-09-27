using System.Text.Json.Serialization;

namespace Meadow.Hcom;

public class PackageVersions
{
    [JsonPropertyName("versions")]
    public string[] Versions { get; set; }
}
