using System.Collections.Generic;
using System.IO;

namespace Meadow.Package;

public partial class PackageManager
{
    private const string PreLinkDirectoryName = "prelink_bin";
    public const string PostLinkDirectoryName = "postlink_bin";
    public const string PackageOutputDirectoryName = "mpak";

    private string _meadowAssembliesPath = string.Empty;

    public List<string> GetDependencies(FileInfo file)
    {
        return _meadowLinker.MapDependencies(file);
    }

    private string GetMeadowAssembliesPath()
    {
        if (string.IsNullOrWhiteSpace(_meadowAssembliesPath))
        {   // for now we only support F7
            // TODO: add switch and support for other platforms
            var store = _fileManager.Firmware["Meadow F7"];
            if (store != null)
            {
                store.Refresh();
                if (store.DefaultPackage != null && store.DefaultPackage.BclFolder != null)
                {
                    _meadowAssembliesPath = store.DefaultPackage.GetFullyQualifiedPath(store.DefaultPackage.BclFolder);
                }
            }
        }
        return _meadowAssembliesPath;
    }
}