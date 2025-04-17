using Meadow.Linker;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Meadow.CLI;
public partial class BuildManager : IBuildManager
{
    public List<string> BuildErrorText { get; } = new();

    public const string PreLinkDirectoryName = "prelink_bin";
    public const string PostLinkDirectoryName = "postlink_bin";
    public const string PackageOutputDirectoryName = "mpak";

    public const string BuildOptionsFileName = "app.build.yaml";

    private readonly FileManager _fileManager;

    public BuildManager(FileManager fileManager)
    {
        _fileManager = fileManager;
    }

    public List<string> GetDependencies(FileInfo file, string? osVerion)
    {
        var linker = new MeadowLinker(GetAssemblyPathForOS(osVerion));
        return linker.MapDependencies(file);
    }

    internal bool CleanApplication(string projectFilePath, string configuration = "Release", CancellationToken? cancellationToken = null)
    {
        using var proc = new Process();
        proc.StartInfo.FileName = "dotnet";
        proc.StartInfo.Arguments = $"clean \"{projectFilePath}\" -c {configuration}";

        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.ErrorDialog = false;
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;

        var success = true;

        proc.ErrorDataReceived += (sendingProcess, errorLine) =>
        {
            Debug.WriteLine(errorLine.Data);
        };
        proc.OutputDataReceived += (sendingProcess, dataLine) =>
        {
            if (dataLine.Data != null)
            {
                Debug.WriteLine(dataLine.Data);
                if (dataLine.Data.ToLower(CultureInfo.InvariantCulture).Contains("clean failed"))
                {
                    Debug.WriteLine("Clean failed");
                    success = false;
                }
            }
        };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc.WaitForExit();
        var exitCode = proc.ExitCode;
        proc.Close();

        return success;
    }

    public bool BuildApplication(string projectFilePath, string configuration = "Release", bool clean = true, CancellationToken? cancellationToken = null)
    {
        BuildErrorText.Clear();

        if (cancellationToken?.IsCancellationRequested == true)
        {
            return false;
        }

        if (clean && !CleanApplication(projectFilePath, configuration, cancellationToken))
        {
            return false;
        }

        using var proc = new Process();
        proc.StartInfo.FileName = "dotnet";
        proc.StartInfo.Arguments = $"build \"{projectFilePath}\" -c \"{configuration}\"";
        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.ErrorDialog = false;
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;

        string lastMessage = string.Empty;
        proc.OutputDataReceived += (sendingProcess, dataLine) =>
        {
            if (dataLine.Data != null)
            {
                BuildErrorText.Add(dataLine.Data);
                Debug.WriteLine(dataLine.Data);
            }
        };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc.WaitForExit();
        var exitCode = proc.ExitCode;
        proc.Close();

        if (exitCode == 0)
        {
            BuildErrorText.Clear();
        }

        return exitCode == 0;
    }

    public Task TrimApplication(
        FileInfo applicationFilePath,
        string osVersion,
        bool includePdbs = false,
        IEnumerable<string>? noLink = null,
        ILogger? logger = null,
        CancellationToken? cancellationToken = null)
    {
        if (!applicationFilePath.Exists)
        {
            throw new FileNotFoundException($"{applicationFilePath} not found");
        }

        // does a meadow.build.yml file exist?
        var buildOptionsFile = Path.Combine(
            applicationFilePath.DirectoryName ?? string.Empty,
            BuildOptionsFileName);

        if (File.Exists(buildOptionsFile))
        {
            var yaml = File.ReadAllText(buildOptionsFile);
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            var opts = deserializer.Deserialize<BuildOptions>(yaml);

            if (opts != null && opts.Deploy != null)
            {
                if (opts.Deploy.NoLink != null && opts.Deploy.NoLink.Count > 0)
                {
                    noLink = opts.Deploy.NoLink;
                }
                if (opts.Deploy.IncludePDBs != null)
                {
                    includePdbs = opts.Deploy.IncludePDBs.Value;
                }
            }
        }

        var linker = new MeadowLinker(GetAssemblyPathForOS(osVersion, logger));

        return linker.Trim(applicationFilePath, includePdbs, noLink);
    }

    public static FileInfo[] GetAvailableBuiltConfigurations(string rootFolder, string appName = "App.dll")
    {
        // check if we were give path to a project file, not the folder of the project file
        if (File.Exists(rootFolder))
        {
            rootFolder = Path.GetDirectoryName(rootFolder) ?? ""; // extreact the folder name or if invalid, use the current directory
        }
        if (!Directory.Exists(rootFolder)) { throw new DirectoryNotFoundException($"Directory not found '{rootFolder}'. Check path to project file."); }

        //see if this is a fully qualified path to the app.dll
        if (File.Exists(Path.Combine(rootFolder, appName)))
        {
            return new FileInfo[] { new(Path.Combine(rootFolder, appName)) };
        }

        // look for a 'bin' folder
        var path = Path.Combine(rootFolder, "bin");
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"No 'bin' directory found under '{rootFolder}'. Have you compiled?");

        var files = new List<FileInfo>();
        FindApp(path, files);

        void FindApp(string directory, List<FileInfo> fileList)
        {
            foreach (var dir in Directory.GetDirectories(directory))
            {
                var shortname = Path.GetFileName(dir);

                if (shortname == PostLinkDirectoryName ||
                    shortname == PreLinkDirectoryName ||
                    shortname == PackageOutputDirectoryName)
                {
                    continue;
                }

                var file = Directory.GetFiles(dir).FirstOrDefault(f => string.Compare(Path.GetFileName(f), appName, true) == 0);
                if (file != null)
                {
                    fileList.Add(new FileInfo(file));
                }

                FindApp(dir, fileList);
            }
        }

        return files.ToArray();
    }

    private string GetAssemblyPathForOS(string? osVersion, ILogger? logger = null)
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

            logger?.Log(LogLevel.Information, $"Found firmware package for Meadow OS v{osVersion}");

            if (package == null)
            {
                throw new Exception($"No firmware package found for Meadow F7 with version {osVersion}");
            }
            return package.GetFullyQualifiedPath(package.BclFolder);
        }

        throw new Exception("No firmware package(s) found for Meadow F7");
    }
}