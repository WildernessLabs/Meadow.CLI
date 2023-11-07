using Microsoft.Extensions.Logging;

namespace Meadow.Cli;

public interface IPackageManager
{
    string? MeadowAssembliesPath { get; }
    string? GetMeadowAssemblyPathForVersion(string runtimeVersion);

    List<string> GetDependencies(FileInfo file, string? bclFolder = null);

    bool BuildApplication(
        string projectFilePath,
        string configuration = "Release",
        bool clean = true,
        ILogger? logger = null,
        CancellationToken? cancellationToken = null);

    Task TrimApplication(
        FileInfo applicationFilePath,
        bool includePdbs = false,
        IList<string>? noLink = null,
        ILogger? logger = null,
        CancellationToken? cancellationToken = null);

    Task<string> AssemblePackage(
        string contentSourceFolder,
        string outputFolder,
        string osVersion,
        string filter = "*",
        bool overwrite = false,
        ILogger? logger = null,
        CancellationToken? cancellationToken = null);

}
