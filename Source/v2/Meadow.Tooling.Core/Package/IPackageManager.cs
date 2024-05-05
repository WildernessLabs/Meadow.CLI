using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Package;

public interface IPackageManager
{
    List<string> GetDependencies(FileInfo file, string? osVerion);

    bool BuildApplication(
        string projectFilePath,
        string configuration = "Release",
        bool clean = true,
        CancellationToken? cancellationToken = null);

    Task TrimApplication(
        FileInfo applicationFilePath,
        string osVerion,
        bool includePdbs = false,
        IEnumerable<string>? noLink = null,
        CancellationToken? cancellationToken = null);

    Task<string> AssemblePackage(string contentSourceFolder,
        string outputFolder,
        string osVersion,
        string? mpakName = null,
        string filter = "*",
        bool overwrite = false,
        CancellationToken? cancellationToken = null);
}