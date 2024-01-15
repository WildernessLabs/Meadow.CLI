using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud collection list", Description = "List Meadow Collections")]
public class CloudCollectionListCommand : BaseCloudCommand<CloudCollectionListCommand>
{
    [CommandOption("host", 'h', Description = $"Optionally set a host (default is {DefaultHost})", IsRequired = false)]
    public string? Host { get; set; }
    [CommandOption("orgId", 'o', Description = "Organization Id", IsRequired = false)]
    public string? OrgId { get; set; }

    public CloudCollectionListCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        Host ??= DefaultHost;
        var org = await ValidateOrg(Host, OrgId, CancellationToken);

        if (org == null) return;

        var collections = await CollectionService.GetOrgCollections(org.Id, Host, CancellationToken);

        if (collections == null || collections.Count == 0)
        {
            Logger?.LogInformation("No collections found.");
        }
        else
        {
            Logger?.LogInformation("Collections:");
            foreach (var collection in collections)
            {
                Logger?.LogInformation($" {collection.Id} | {collection.Name}");
            }
        }
    }
}