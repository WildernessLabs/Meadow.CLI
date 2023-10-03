using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud collection list", Description = "List Meadow Collections")]
public class CloudCollectionListCommand : BaseCloudCommand<CloudCollectionListCommand>
{
    public const string DefaultHost = "https://www.meadowcloud.co";

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
    {
    }

    protected override async ValueTask ExecuteCommand(CancellationToken? cancellationToken)
    {
        UserOrg? org;

        try
        {
            if (Host == null) Host = DefaultHost;

            Logger?.LogInformation("Retrieving your user and organization information...");

            var userOrgs = await UserService.GetUserOrgs(Host, cancellationToken).ConfigureAwait(false);
            if (!userOrgs.Any())
            {
                Logger?.LogInformation($"Please visit {Host} to register your account.");
                return;
            }
            else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
            {
                Logger?.LogInformation($"You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.");
                return;
            }
            else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
            {
                OrgId = userOrgs.First().Id;
            }

            org = userOrgs.FirstOrDefault(o => o.Id == OrgId || o.Name == OrgId);
            if (org == null)
            {
                Logger?.LogInformation($"Unable to find an organization with a Name or ID matching '{OrgId}'");
                return;
            }
        }
        catch (MeadowCloudAuthException)
        {
            Logger?.LogError($"You must be signed in to execute this command.");
            Logger?.LogError($"Please run \"meadow cloud login\" to sign in to Meadow.Cloud.");
            return;
        }

        var collections = await CollectionService.GetOrgCollections(org.Id, Host, cancellationToken);

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
