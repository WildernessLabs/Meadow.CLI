using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI;

public interface IBuildManager
{
    List<string> BuildErrorText { get; }

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
}