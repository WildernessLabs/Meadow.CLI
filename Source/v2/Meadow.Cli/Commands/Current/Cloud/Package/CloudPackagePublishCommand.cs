using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud package publish", Description = "Publishes a Meadow Package (MPAK)")]
public class CloudPackagePublishCommand : BaseCloudCommand<CloudPackagePublishCommand>
{
    private readonly PackageService _packageService;

    [CommandParameter(0, Name = "PackageID", Description = "ID of the package to publish", IsRequired = true)]
    public string PackageId { get; init; } = string.Empty;

    [CommandOption("collectionId", 'c', Description = "The target collection for publishing", IsRequired = true)]
    public string CollectionId { get; set; } = string.Empty;

    [CommandOption("metadata", 'm', Description = "Pass through metadata", IsRequired = false)]
    public string? Metadata { get; set; }

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    public CloudPackagePublishCommand(
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

        try
        {
            Logger?.LogInformation($"Publishing package {PackageId} to collection {CollectionId}...");

            await _packageService.PublishPackage(PackageId, CollectionId, Metadata ?? string.Empty, Host, CancellationToken);
            Logger?.LogInformation("Publish successful.");
        }
        catch (MeadowCloudException mex)
        {
            Logger?.LogError($"Publish failed: {mex.Message}");
        }
    }
}