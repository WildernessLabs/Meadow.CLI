using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCloudCommand<T> : BaseCommand<T>
{
    public const string DefaultHost = "https://www.meadowcloud.co";

    protected IdentityManager IdentityManager { get; }
    protected UserService UserService { get; }
    protected DeviceService DeviceService { get; }
    protected CollectionService CollectionService { get; }

    public BaseCloudCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        ILoggerFactory? loggerFactory)
        : base(loggerFactory)
    {
        IdentityManager = identityManager;
        UserService = userService;
        DeviceService = deviceService;
        CollectionService = collectionService;
    }

    protected async Task<UserOrg?> ValidateOrg(string host, string? orgNameOrId = null, CancellationToken? cancellationToken = null)
    {
        try
        {
            Logger?.LogInformation("Retrieving your user and organization information...");

            var userOrgs = await UserService.GetUserOrgs(host, cancellationToken).ConfigureAwait(false);
            if (!userOrgs.Any())
            {
                Logger?.LogInformation($"Please visit {host} to register your account.");
                return null;
            }
            else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(orgNameOrId))
            {
                Logger?.LogInformation($"You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.");
                return null;
            }
            else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(orgNameOrId))
            {
                orgNameOrId = userOrgs.First().Id;
            }

            var org = userOrgs.FirstOrDefault(o => o.Id == orgNameOrId || o.Name == orgNameOrId);
            if (org == null)
            {
                Logger?.LogInformation($"Unable to find an organization with a Name or ID matching '{orgNameOrId}'");
            }

            return org;
        }
        catch (MeadowCloudAuthException)
        {
            Logger?.LogError($"You must be signed into your Wilderness Labs account to execute this command. Run 'meadow cloud login' to do so.");
        }

        return null;
    }
}