using System.Collections.Generic;

namespace Meadow.Software;

/// <summary>
/// The <c>manifest.json</c> published with each firmware release.
/// </summary>
public class F7ReleaseMetadata
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = default!;
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
    [JsonPropertyName("generation")]
    public string? Generation { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("minCLIVersion")]
    public string MinCLIVersion { get; set; } = default!;
    [JsonPropertyName("files")]
    public Dictionary<string, F7ReleaseFile> Files { get; set; } = new();
}

/// <summary>
/// A file attached to a firmware release, as listed in its manifest.
/// </summary>
public class F7ReleaseFile
{
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = default!;
}
