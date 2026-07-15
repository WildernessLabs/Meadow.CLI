using CliFx.Attributes;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package create", Description = "Create a Meadow Package (MPAK)")]
public class CloudPackageCreateCommand : BaseCommand<CloudPackageCreateCommand>
{
    [CommandParameter(0, Description = "Path to project file", IsRequired = false)]
    public string? ProjectPath { get; init; }

    [CommandOption("configuration", 'c', Description = "The build configuration to compile", IsRequired = false)]
    public string Configuration { get; set; } = "Release";

    [CommandOption("name", 'n', Description = "Name of the mpak file to be created", IsRequired = false)]
    public string? MpakName { get; init; }

    [CommandOption("filter", 'f', Description = "Glob pattern to filter files. ex ('app.dll', 'app*','{app.dll,meadow.dll}')",
        IsRequired = false)]
    public string Filter { get; init; } = "**/*";

    [CommandOption("osVersion", 'v', Description = "Target OS version for the app", IsRequired = false)]
    public string? OsVersion { get; init; } = default!;

    private readonly IPackageManager _packageManager;
    private readonly FileManager _fileManager;

    public CloudPackageCreateCommand(
        IPackageManager packageManager,
        FileManager fileManager,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _packageManager = packageManager;
        _fileManager = fileManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        await _fileManager.Refresh();
        var store = _fileManager.Firmware["Meadow F7"];
        await ValidateFirmwarePackage(store);

        var osVersion = OsVersion ?? store!.DefaultPackage!.Version;

        var projectPath = ProjectPath ?? AppTools.ValidateAndSanitizeAppPath(ProjectPath);

        string sourceDir;
        string packageDir;

        if (MeadowVersion.IsV3OrLater(osVersion))
        {
            // Meadow 3.x: dotnet publish handles trimming via the project's built-in linker
            Logger?.LogInformation($"Publishing {Configuration} configuration of {projectPath} (Meadow v3)...");
            var success = _packageManager.PublishApplication(projectPath, osVersion, Configuration, logger: Logger);
            if (!success)
            {
                throw new CommandException("Publish failed", CommandExitCode.GeneralError);
            }

            var buildPath = GetAppBuildPath(projectPath);
            // After publish, GetAppBuildPath returns the directory containing the newest App.dll,
            // which is the publish/ folder itself — don't append another segment.
            var publishDir = Path.GetFileName(buildPath) == "publish"
                ? buildPath
                : Path.Combine(buildPath, "publish");
            // Place the .mpak alongside the project's bin/, not inside publish/, so a clean wipes it.
            var packageBaseDir = Path.GetFileName(buildPath) == "publish"
                ? Path.GetDirectoryName(buildPath)!
                : buildPath;
            if (!Directory.Exists(publishDir))
            {
                throw new CommandException($"Cannot find publish output at '{publishDir}'", CommandExitCode.GeneralError);
            }

            sourceDir = publishDir;
            packageDir = Path.Combine(packageBaseDir, PackageManager.PackageOutputDirectoryName);
        }
        else
        {
            // Meadow 2.x: dotnet build + custom ILLink trimming
            BuildApp(projectPath);

            var buildPath = GetAppBuildPath(projectPath);

            await AppTools.TrimApplication(projectPath, _packageManager, osVersion, Configuration, null, Logger, Console, CancellationToken);
            Logger.LogInformation(string.Format(Strings.TrimmedApplicationForSpecifiedVersion, osVersion));

            var postlinkDir = Path.Combine(buildPath, PackageManager.PostLinkDirectoryName);

            // copy non-assembly files to the postlink directory
            CopyContentFiles(buildPath, postlinkDir);

            sourceDir = postlinkDir;
            packageDir = Path.Combine(buildPath, PackageManager.PackageOutputDirectoryName);
        }

        Logger.LogInformation(Strings.AssemblingCloudPackage);
        var packagePath = await _packageManager.AssemblePackage(sourceDir, packageDir, osVersion, MpakName, Filter, true, CancellationToken);

        if (packagePath != null)
        {
            Logger.LogInformation(string.Format(Strings.PackageAvailableAtSpecifiedPath, packagePath));
        }
        else
        {
            throw new CommandException(Strings.PackageAssemblyFailed);
        }
    }

    private async Task ValidateFirmwarePackage(IFirmwarePackageCollection? collection)
    {
        await _fileManager.Refresh();

        // for now we only support F7
        if (collection == null || collection.Count() == 0)
        {
            throw new CommandException(Strings.NoFirmwarePackagesFound, CommandExitCode.GeneralError);
        }

        if (collection.DefaultPackage == null)
        {
            throw new CommandException(Strings.NoDefaultFirmwarePackageSet, CommandExitCode.GeneralError);
        }
    }

    private void BuildApp(string path)
    {
        Configuration ??= "Release";

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        var success = _packageManager.BuildApplication(path, Configuration);

        if (!success)
        {
            throw new CommandException("Build failed", CommandExitCode.GeneralError);
        }
        else
        {
            Logger?.LogInformation($"Build successful");
        }
    }

    private string GetAppBuildPath(string path)
    {
        var candidates = PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return path;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        //get the directory of the file
        return Path.GetDirectoryName(file.FullName);
    }

    /// <summary>
    /// Copy all files that are not assemblies in the source directory to the target directory
    //  and ignore the prelink, postlink, and package output directories if they're in the source directory
    /// </summary>
    /// <param name="sourceDir">Source, the compiled output folder</param>
    /// <param name="targetDir">The target folder</param>
    private void CopyContentFiles(string sourceDir, string targetDir)
    {
        var sourceFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);

        foreach (var sourceFile in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var targetFile = Path.Combine(targetDir, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetFile);

            if (sourceFile.Contains(PackageManager.PostLinkDirectoryName) ||
                sourceFile.Contains(PackageManager.PreLinkDirectoryName) ||
                sourceFile.Contains(PackageManager.PackageOutputDirectoryName))
            {
                continue;
            }

            if (targetDirectory != null && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (Path.GetExtension(sourceFile) == ".dll" ||
                Path.GetExtension(sourceFile) == ".exe" ||
                Path.GetExtension(sourceFile) == ".pdb")
            {
                continue;
            }

            File.Copy(sourceFile, targetFile, true);
        }
    }
}