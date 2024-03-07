using CliFx.Attributes;
using Meadow.Cloud.Client;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud apikey list", Description = "List your Meadow.Cloud API keys")]
public class CloudApiKeyListCommand : BaseCloudCommand<CloudApiKeyListCommand>
{
    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyListCommand(
        IMeadowCloudClient meadowCloudClient,
        ApiTokenService apiTokenService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        ApiTokenService = apiTokenService;
    }

    protected override ValueTask PreAuthenticatedValidation()
    {
        Logger.LogInformation($"Retrieving your API keys from Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask ExecuteCloudCommand()
    {
        try
        {
            var response = await ApiTokenService.GetApiTokens(Host, CancellationToken);
            var apiTokens = response.OrderBy(a => a.Name);

            if (!apiTokens.Any())
            {
                Logger.LogInformation("You have no API keys.");
                return;
            }

            var table = new ConsoleTable("Id", "Name", $"Expires (UTC)", "Scopes");
            foreach (var apiToken in apiTokens)
            {
                table.AddRow(apiToken.Id, apiToken.Name, $"{apiToken.ExpiresAt:G}", string.Join(", ", apiToken.Scopes.OrderBy(t => t)));
            }

            Logger.LogInformation(table);
        }
        catch (MeadowCloudException ex)
        {
            throw new CommandException($"Get API keys command failed: {ex.Message}", innerException: ex);
        }
    }
}


