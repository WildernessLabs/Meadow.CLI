using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package create", Description = "Create a Meadow Package (MPAK)")]
public class CloudPackageCreateCommand : BaseCloudCommand<CloudPackageCreateCommand>
{
    [CommandParameter(0, Name = "Path to project file", IsRequired = false)]
    public string? ProjectPath { get; set; } = default!;

    [CommandOption('c', Description = "The build configuration to compile", IsRequired = false)]
    public string Configuration { get; set; } = "Release";

    [CommandOption("name", 'n', Description = "Name of the mpak file to be created", IsRequired = false)]
    public string? MpakName { get; init; } = default!;

    [CommandOption("filter", 'f', Description = "Glob pattern to filter files. ex ('app.dll', 'app*','{app.dll,meadow.dll}')",
        IsRequired = false)]
    public string Filter { get; init; } = "*";

    private IPackageManager _packageManager;

    public CloudPackageCreateCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        IPackageManager packageManager,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand(CancellationToken? cancellationToken)
    {
        if (ProjectPath == null)
        {
            ProjectPath = AppDomain.CurrentDomain.BaseDirectory;
        }

        // build
        Logger?.LogInformation($"Building {Configuration} version of application...");
        if (!_packageManager.BuildApplication(ProjectPath, Configuration, true, cancellationToken))
        {
            return;
        }

        var candidates = PackageManager.GetAvailableBuiltConfigurations(ProjectPath, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{ProjectPath}'");
            return;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();        // trim
        Logger?.LogInformation($"Trimming application...");
        await _packageManager.TrimApplication(file, cancellationToken: cancellationToken);

        // package
        var packageDir = Path.Combine(file.Directory?.FullName ?? string.Empty, PackageManager.PackageOutputDirectoryName);
        var postlinkDir = Path.Combine(file.Directory?.FullName ?? string.Empty, PackageManager.PostLinkDirectoryName);

        Logger?.LogInformation($"Assembling the MPAK...");
        var packagePath = await _packageManager.AssemblePackage(postlinkDir, packageDir, Filter, true, cancellationToken);

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
