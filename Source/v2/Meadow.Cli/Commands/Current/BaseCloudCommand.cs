using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCloudCommand<T> : BaseCommand<T>
{
    [CommandOption("host", Description = $"The Meadow.Cloud endpoint.", IsRequired = false)]
    public string Host { get; set; } = DefaultHost;

    protected const string DefaultHost = Meadow.Cloud.Client.MeadowCloudClient.DefaultHost;

    protected bool RequiresAuthentication { get; set; } = true;

    protected IMeadowCloudClient MeadowCloudClient { get; }
    protected UserService UserService { get; }
    

    public BaseCloudCommand(
        IMeadowCloudClient meadowCloudClient,
        UserService userService,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        MeadowCloudClient = meadowCloudClient;
        UserService = userService;
    }

    protected virtual ValueTask PreAuthenticatedValidation()
    {
        return ValueTask.CompletedTask;
    }

    protected abstract ValueTask ExecuteCloudCommand();

    protected sealed override async ValueTask ExecuteCommand()
    {
        await PreAuthenticatedValidation();

        if (RequiresAuthentication)
        {
            var result = await MeadowCloudClient.Authenticate(CancellationToken);
            if (!result)
            {
                throw new CommandException("You must be signed into your Wilderness Labs account to execute this command. Run 'meadow login' to do so.");
            }

            // If the user does not yet exist in Meadow.Cloud, this creates them and sets up their initial org
            var _ = await UserService.GetMe(Host, CancellationToken)
                ?? throw new CommandException("There was a problem retrieving your account information.");
        }

        try
        {
            await ExecuteCloudCommand();
        }
        catch (MeadowCloudAuthException ex)
        {
            throw new CommandException("You must be signed into your Wilderness Labs account to execute this command. Run 'meadow login' to do so.", ex);
        }
    }

    protected async Task<UserOrg?> GetOrg(string host, string? orgNameOrId = null, CancellationToken? cancellationToken = null)
    {
        Logger.LogInformation("Retrieving your user and organization information...");

        var userOrgs = await UserService.GetUserOrgs(host, cancellationToken).ConfigureAwait(false);
        if (userOrgs.Count > 1 && string.IsNullOrEmpty(orgNameOrId))
        {
            Logger.LogInformation($"You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.");
            return null;
        }
        else if (userOrgs.Count == 1 && string.IsNullOrEmpty(orgNameOrId))
        {
            orgNameOrId = userOrgs[0].Id;
        }

        var org = userOrgs.FirstOrDefault(o => o.Id == orgNameOrId || o.Name == orgNameOrId);
        if (org == null)
        {
            Logger.LogInformation($"Unable to find an organization with a Name or ID matching '{orgNameOrId}'");
        }

        return org;
    }
}