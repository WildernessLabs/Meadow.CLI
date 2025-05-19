using CliFx.Attributes;
using Meadow.Cloud.Client;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud apikey delete", Description = "Delete a Meadow.Cloud API key")]
public class CloudApiKeyDeleteCommand : BaseCloudCommand<CloudApiKeyDeleteCommand>
{
    [CommandParameter(0, Description = "The name or ID of the API key", IsRequired = true, Name = "NAME_OR_ID")]
    public string NameOrId { get; init; } = default!;

    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyDeleteCommand(
        IMeadowCloudClient meadowCloudClient,
        ApiTokenService apiTokenService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        ApiTokenService = apiTokenService;
    }

    protected override ValueTask PreAuthenticatedValidation()
    {
        Logger.LogInformation($"Deleting API key `{NameOrId}` on Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");
        return ValueTask.CompletedTask;
    }

    protected async override ValueTask ExecuteCloudCommand()
    {
        var getRequest = await ApiTokenService.GetApiTokens(Host, CancellationToken);
        var apiKey = getRequest.FirstOrDefault(x => x.Id == NameOrId || string.Equals(x.Name, NameOrId, StringComparison.OrdinalIgnoreCase));

        if (apiKey == null)
        {
            throw new CommandException($"API key `{NameOrId}` not found.");
        }

        await ApiTokenService.DeleteApiToken(apiKey.Id, Host, CancellationToken);
    }
}
