using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package list", Description = "Lists all Meadow Packages (MPAK)")]
public class CloudPackageListCommand : BaseCloudCommand<CloudPackageListCommand>
{
    private readonly PackageService _packageService;

    [CommandOption("orgId", 'o', Description = "Optional organization ID", IsRequired = false)]
    public string? OrgId { get; set; }

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    public CloudPackageListCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        PackageService packageService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
        _packageService = packageService;
    }

    protected override async ValueTask ExecuteCommand()
    {
        Host ??= DefaultHost;
        var org = await ValidateOrg(Host, OrgId, CancellationToken);

        if (org == null) { return; }

        var packages = await _packageService.GetOrgPackages(org.Id, Host, CancellationToken);

        if (packages == null || packages.Count == 0)
        {
            Logger?.LogInformation("No packages found");
        }
        else
        {
            Logger?.LogInformation("packages:");
            foreach (var package in packages)
            {
                Logger?.LogInformation($" {package.Id} | {package.Name}");
            }
        }
    }
}