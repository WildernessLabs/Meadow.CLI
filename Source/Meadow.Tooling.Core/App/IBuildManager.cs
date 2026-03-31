using Microsoft.Extensions.Logging;
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
        ILogger? logger = null,
        CancellationToken? cancellationToken = null);

    /// <summary>
    /// Builds and publishes the application using dotnet publish.
    /// For Meadow OS 3.x+, the project's built-in trimming is used instead of custom ILLink.
    /// Injects Meadow's custom BCL assemblies into the trimming pipeline.
    /// </summary>
    bool PublishApplication(
        string projectFilePath,
        string osVersion,
        string configuration = "Release",
        bool clean = true,
        CancellationToken? cancellationToken = null);
}