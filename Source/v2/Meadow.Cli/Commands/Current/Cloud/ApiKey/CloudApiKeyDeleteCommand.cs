using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
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
        UserService userService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, userService, loggerFactory)
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
        try
        {
            var getRequest = await ApiTokenService.GetApiTokens(Host, CancellationToken);
            var apiKey = getRequest.FirstOrDefault(x => x.Id == NameOrId || string.Equals(x.Name, NameOrId, StringComparison.OrdinalIgnoreCase));

            if (apiKey == null)
            {
                throw new CommandException($"API key `{NameOrId}` not found.");
            }

            await ApiTokenService.DeleteApiToken(apiKey.Id, Host, CancellationToken);
        }
        catch (MeadowCloudException ex)
        {
            throw new CommandException($"Create API key command failed: {ex.Message}", innerException: ex);
        }
    }
}
