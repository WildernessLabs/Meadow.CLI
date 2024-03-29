﻿using CliFx.Attributes;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package create", Description = "Create a Meadow Package (MPAK)")]
public class CloudPackageCreateCommand : BaseCommand<CloudPackageCreateCommand>
{
    [CommandParameter(0, Description = "Path to project file", IsRequired = false)]
    public string? ProjectPath { get; set; }

    [CommandOption("configuration", 'c', Description = "The build configuration to compile", IsRequired = false)]
    public string Configuration { get; init; } = "Release";

    [CommandOption("name", 'n', Description = "Name of the mpak file to be created", IsRequired = false)]
    public string? MpakName { get; init; }

    [CommandOption("filter", 'f', Description = "Glob pattern to filter files. ex ('app.dll', 'app*','{app.dll,meadow.dll}')",
        IsRequired = false)]
    public string Filter { get; init; } = "*";

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
        ProjectPath ??= Directory.GetCurrentDirectory();
        ProjectPath = Path.GetFullPath(ProjectPath);
        if (!Directory.Exists(ProjectPath))
        {
            throw new CommandException($"Directory not found '{ProjectPath}'. Check path to project file.", CommandExitCode.DirectoryNotFound);
        }

        // build
        Logger.LogInformation(string.Format(Strings.BuildingSpecifiedConfiguration, Configuration));
        if (!_packageManager.BuildApplication(ProjectPath, Configuration, true, CancellationToken))
        {
            throw new CommandException(Strings.BuildFailed);
        }

        var candidates = PackageManager.GetAvailableBuiltConfigurations(ProjectPath, "App.dll");

        if (candidates.Length == 0)
        {
            throw new CommandException($"Cannot find a compiled application at '{ProjectPath}'", CommandExitCode.FileNotFound);
        }

        var store = _fileManager.Firmware["Meadow F7"];
        await store.Refresh();
        var osVersion = OsVersion ?? store?.DefaultPackage?.Version ?? "unknown";

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();
        // trim
        Logger.LogInformation(string.Format(Strings.TrimmingApplicationForSpecifiedVersion, osVersion));
        await _packageManager.TrimApplication(file, cancellationToken: CancellationToken);

        // package
        var packageDir = Path.Combine(file.Directory?.FullName ?? string.Empty, PackageManager.PackageOutputDirectoryName);
        //TODO - properly manage shared paths
        var postlinkDir = Path.Combine(file.Directory?.FullName ?? string.Empty, PackageManager.PostLinkDirectoryName);

        Logger.LogInformation(Strings.AssemblingCloudPackage);
        var packagePath = await _packageManager.AssemblePackage(postlinkDir, packageDir, osVersion, MpakName, Filter, true, CancellationToken);

        if (packagePath != null)
        {
            Logger.LogInformation(string.Format(Strings.PackageAvailableAtSpecifiedPath, packagePath));
        }
        else
        {
            throw new CommandException(Strings.PackageAssemblyFailed);
        }
    }
}