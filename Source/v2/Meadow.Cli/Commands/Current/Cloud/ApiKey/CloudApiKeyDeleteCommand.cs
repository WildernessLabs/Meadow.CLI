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

    [CommandOption("host", Description = $"Optionally set a host (default is {DefaultHost})", IsRequired = false)]
    public string? Host { get; set; }

    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyDeleteCommand(
        ApiTokenService apiTokenService,
        CollectionService collectionService,
        DeviceService deviceService,
        IdentityManager identityManager,
        UserService userService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
        ApiTokenService = apiTokenService;
    }

    protected async override ValueTask ExecuteCommand()
    {
        Host ??= DefaultHost;

        Logger?.LogInformation($"Deleting API key `{NameOrId}` on Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");

        var token = await IdentityManager.GetAccessToken(CancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CommandException("You must be signed into Meadow.Cloud to execute this command. Run 'meadow cloud login' to do so.");
        }

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
        catch (MeadowCloudAuthException ex)
        {
            throw new CommandException("You must be signed in to execute this command.", innerException: ex);
        }
        catch (MeadowCloudException ex)
        {
            throw new CommandException($"Create API key command failed: {ex.Message}", innerException: ex);
        }
    }
}
