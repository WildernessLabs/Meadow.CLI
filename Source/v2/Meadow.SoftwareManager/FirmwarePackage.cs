using System.IO;

namespace Meadow.Software;

public class FirmwarePackage
{
    internal IFirmwarePackageCollection _collection;

    internal FirmwarePackage(IFirmwarePackageCollection collection)
    {
        _collection = collection;
    }

    public string GetFullyQualifiedPath(string? file)
    {
        return Path.Combine(_collection.PackageFileRoot, Version, file);
    }

    public string Version { get; set; }
    public string Targets { get; set; }
    public string? CoprocBootloader { get; set; }
    public string? CoprocPartitionTable { get; set; }
    public string? CoprocApplication { get; set; }
    public string? OSWithBootloader { get; set; }
    public string? OsWithoutBootloader { get; set; }
    public string? Runtime { get; set; }
    public string? BclFolder { get; set; }
}