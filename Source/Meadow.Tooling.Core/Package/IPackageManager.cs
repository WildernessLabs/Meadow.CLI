using Meadow.CLI;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Package;

public interface IPackageManager : IBuildManager
{
    Task<string> AssemblePackage(string contentSourceFolder,
        string outputFolder,
        string osVersion,
        string? mpakName = null,
        string filter = "*",
        bool overwrite = false,
        CancellationToken? cancellationToken = null);
}