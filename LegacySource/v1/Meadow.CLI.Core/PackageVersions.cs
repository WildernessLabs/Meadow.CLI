using System.Text.Json.Serialization;

namespace Meadow.CLI.Core
{
    public class PackageVersions
    {
        [JsonPropertyName("versions")]
        public string[] Versions { get; set; }
    }
}
