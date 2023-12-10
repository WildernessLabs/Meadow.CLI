using LinkerTest;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI;

public partial class PackageManager
{
    public const string PostLinkDirectoryName = "postlink_bin";
    private const string PreLinkDirectoryName = "prelink_bin";
    public const string PackageOutputDirectoryName = "mpak";

    private string? _meadowAssembliesPath;

    private string? MeadowAssembliesPath
    {
        get
        {
            if (_meadowAssembliesPath == null)
            {
                // for now we only support F7
                // TODO: add switch and support for other platforms
                var store = _fileManager.Firmware["Meadow F7"];
                if (store != null)
                {
                    store.Refresh();
                    if (store.DefaultPackage != null)
                    {
                        var defaultPackage = store.DefaultPackage;

                        if (defaultPackage.BclFolder != null)
                        {
                            _meadowAssembliesPath = defaultPackage.GetFullyQualifiedPath(defaultPackage.BclFolder);
                        }
                    }
                }
            }

            return _meadowAssembliesPath;
        }
    }

    public List<string> GetDependencies(FileInfo file)
    {
        return _meadowLinker.MapDependencies(file);
    }


    public Task TrimDependencies(FileInfo file, IList<string>? noLink, ILogger? logger, bool includePdbs)
    {
        var linker = new MeadowLinker(MeadowAssembliesPath, logger);

        return linker.Trim(file, includePdbs, noLink);
    }
}