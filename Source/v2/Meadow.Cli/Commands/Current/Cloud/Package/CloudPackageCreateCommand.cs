using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package create", Description = "Create a Meadow Package (MPAK)")]
public class CloudPackageCreateCommand : BaseCloudCommand<CloudPackageCreateCommand>
{
    [CommandParameter(0, Name = "Path to project file", IsRequired = false)]
    public string? ProjectPath { get; set; }

    [CommandOption('c', Description = "The build configuration to compile", IsRequired = false)]
    public string Configuration { get; init; } = "Release";

    [CommandOption("name", 'n', Description = "Name of the mpak file to be created", IsRequired = false)]
    public string? MpakName { get; init; }

    [CommandOption("filter", 'f', Description = "Glob pattern to filter files. ex ('app.dll', 'app*','{app.dll,meadow.dll}')",
        IsRequired = false)]
    public string Filter { get; init; } = "*";

    private readonly IPackageManager _packageManager;
    private readonly FileManager _fileManager;

    public CloudPackageCreateCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        IPackageManager packageManager,
        FileManager fileManager,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
        _packageManager = packageManager;
        _fileManager = fileManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        ProjectPath ??= AppDomain.CurrentDomain.BaseDirectory;
        ProjectPath = Path.GetFullPath(ProjectPath);
        if (!Directory.Exists(ProjectPath))
        {
            throw new DirectoryNotFoundException($"Directory not found '{ProjectPath}'. Check path to project file.");
        }

        // build
        Logger?.LogInformation($"Building {Configuration} version of application...");
        if (!_packageManager.BuildApplication(ProjectPath, Configuration, true, CancellationToken))
        {
            return;
        }

        var candidates = PackageManager.GetAvailableBuiltConfigurations(ProjectPath, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{ProjectPath}'");
            return;
        }

        var store = _fileManager.Firmware["Meadow F7"];
        await store.Refresh();
        var osVersion = store?.DefaultPackage?.Version ?? "unknown";

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();        // trim
        Logger?.LogInformation($"Trimming application...");
        await _packageManager.TrimApplication(file, cancellationToken: CancellationToken);

        // package
        var packageDir = Path.Combine(file.Directory?.FullName ?? string.Empty, PackageManager.PackageOutputDirectoryName);
        //TODO - properly manage shared paths
        var postlinkDir = Path.Combine(file.Directory?.FullName ?? string.Empty, PackageManager.PostLinkDirectoryName);

        Logger?.LogInformation($"Assembling the MPAK...");
        var packagePath = await _packageManager.AssemblePackage(postlinkDir, packageDir, osVersion, Filter, true, CancellationToken);

        if (packagePath != null)
        {
            Logger?.LogInformation($"Done. Package is available at {packagePath}");
        }
        else
        {
            Logger?.LogError($"Package assembly failed.");
        }
    }
}