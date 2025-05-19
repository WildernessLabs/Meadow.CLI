using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Users;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseCloudCommand<T> : BaseCommand<T>
{
    [CommandOption("host", Description = $"The Meadow.Cloud endpoint.", IsRequired = false)]
    public string Host { get; set; } = DefaultHost;

    [CommandOption("apikey", Description = "The API key to use with Meadow.Cloud. Otherwise, use the logged in Wilderness Labs account.", EnvironmentVariable = "MC_APIKEY", IsRequired = false)]
    public string? ApiKey { get; set; }

    protected const string DefaultHost = Meadow.Cloud.Client.MeadowCloudClient.DefaultHost;

    protected bool RequiresAuthentication { get; set; } = true;

    protected IMeadowCloudClient MeadowCloudClient { get; }

    public BaseCloudCommand(
        IMeadowCloudClient meadowCloudClient,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        MeadowCloudClient = meadowCloudClient;
    }

    protected virtual ValueTask PreAuthenticatedValidation()
    {
        return ValueTask.CompletedTask;
    }

    protected abstract ValueTask ExecuteCloudCommand();

    protected sealed override async ValueTask ExecuteCommand()
    {
        if (!Host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !Host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new CommandException("Host (--host) must be a valid URL that starts with http:// or https://.");
        }

        if (!Uri.TryCreate(Host, UriKind.Absolute, out Uri? baseAddress) || baseAddress == null)
        {
            throw new CommandException("Host (--host) must be a valid URL.");
        }
        
        MeadowCloudClient.BaseAddress = baseAddress;

        await PreAuthenticatedValidation();

        if (RequiresAuthentication)
        {
            if (!string.IsNullOrEmpty(ApiKey))
            {
                MeadowCloudClient.Authorization = new AuthenticationHeaderValue("APIKEY", ApiKey);
            }
            else
            {
                var result = await MeadowCloudClient.Authenticate(CancellationToken);
                if (!result)
                {
                    throw new CommandException("You must be signed into your Wilderness Labs account to execute this command. Run 'meadow login' to do so.");
                }

                // If the user does not yet exist in Meadow.Cloud, this creates them and sets up their initial org
                var _ = await MeadowCloudClient.User.GetUser(CancellationToken)
                    ?? throw new CommandException("There was a problem retrieving your account information.");
            }
        }

        try
        {
            await ExecuteCloudCommand();
        }
        catch (MeadowCloudAuthException ex)
        {
            throw new CommandException("You must be signed into your Wilderness Labs account to execute this command. Run 'meadow login' to do so.", ex);
        }
        catch (MeadowCloudException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var sb = new StringBuilder("You are not authorized to perform this action. Please check that you have sufficient access");
                if (!string.IsNullOrWhiteSpace(ApiKey))
                {
                    sb.Append(", that your API keys is valid with the correct scopes,");
                }
                sb.Append(" and try again.");

                throw new CommandException(sb.ToString(), ex);
            }

            throw new CommandException($@"There was a problem executing the command. Meadow.Cloud returned a non-successful response.

{(int)ex.StatusCode} {ex.StatusCode}
Response: {(string.IsNullOrWhiteSpace(ex.Response) ? "None" : Environment.NewLine + ex.Response)}

{ex.StackTrace}", ex);
        }
    }

    protected async Task<GetOrganizationResponse?> GetOrganization(string? orgNameOrId = null, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Retrieving your user and organization information...");

        var orgs = await MeadowCloudClient.User.GetOrganizations(cancellationToken).ConfigureAwait(false);
        if (orgs.Count() > 1 && string.IsNullOrEmpty(orgNameOrId))
        {
            Logger.LogInformation($"You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.");
            return null;
        }
        else if (orgs.Count() == 1 && string.IsNullOrEmpty(orgNameOrId))
        {
            orgNameOrId = orgs.Single().Id;
        }

        var org = orgs.FirstOrDefault(o => o.Id == orgNameOrId || string.Equals(o.Name, orgNameOrId, StringComparison.OrdinalIgnoreCase));
        if (org == null)
        {
            Logger.LogInformation($"Unable to find an organization with a Name or ID matching '{orgNameOrId}'");
        }

        return org;
    }
}