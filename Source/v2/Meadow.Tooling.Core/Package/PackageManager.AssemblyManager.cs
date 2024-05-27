using System;
using System.Collections.Generic;
using System.IO;

namespace Meadow.Package;

public partial class PackageManager
{
    public const string PreLinkDirectoryName = "prelink_bin";
    public const string PostLinkDirectoryName = "postlink_bin";
    public const string PackageOutputDirectoryName = "mpak";

    public List<string> GetDependencies(FileInfo file, string? osVerion)
    {
        var linker = new Linker.MeadowLinker(GetAssemblyPathForOS(osVerion));
        return linker.MapDependencies(file);
    }

    private string GetAssemblyPathForOS(string? osVersion)
    {
        if (string.IsNullOrWhiteSpace(osVersion))
        {
            osVersion = _fileManager?.Firmware["Meadow F7"]?.DefaultPackage?.Version;
        }

        var store = _fileManager?.Firmware["Meadow F7"];
        if (store != null)
        {
            store.Refresh();

            var package = store.GetClosestLocalPackage(osVersion!);

            if (package == null)
            {
                throw new Exception($"No firmware package found for Meadow F7 with version {osVersion}");
            }
            return package.GetFullyQualifiedPath(package.BclFolder);
        }

        throw new Exception("No firmware package(s) found for Meadow F7");
    }
}