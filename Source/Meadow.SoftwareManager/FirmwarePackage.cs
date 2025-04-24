using System.IO;

namespace Meadow.Software;

public class FirmwarePackage
{
    private string _rawVersion;
    internal IFirmwarePackageCollection _collection;

    internal FirmwarePackage(IFirmwarePackageCollection collection)
    {
        _collection = collection;
    }

    public string GetFullyQualifiedPath(string? file)
    {
        return Path.Combine(_collection.PackageFileRoot, Version, file);
    }

    public string Version
    {
        get => _rawVersion;
        set
        {
            if (!System.Version.TryParse(value, out var _))
            {
                throw new System.ArgumentException($"Invalid version string: {value}");
            }
            _rawVersion = value;
        }
    }
    public string Targets { get; set; }
    public string? CoprocBootloader { get; set; }
    public string? CoprocPartitionTable { get; set; }
    public string? CoprocApplication { get; set; }
    public string? OSWithBootloader { get; set; }
    public string? OsWithoutBootloader { get; set; }
    public string? Runtime { get; set; }
    public string? BclFolder { get; set; }
}