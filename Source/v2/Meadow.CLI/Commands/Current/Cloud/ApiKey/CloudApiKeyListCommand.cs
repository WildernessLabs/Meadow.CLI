using CliFx.Attributes;
using CliFx.Exceptions;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud apikey list", Description = "List your Meadow.Cloud API keys")]
public class CloudApiKeyListCommand : BaseCloudCommand<CloudApiKeyListCommand>
{
    [CommandOption("host", Description = $"Optionally set a host (default is {DefaultHost})", IsRequired = false)]
    public string? Host { get; set; }

    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyListCommand(
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

    protected override async ValueTask ExecuteCommand()
    {
        Host ??= DefaultHost;

        Logger?.LogInformation($"Retrieving your API keys from Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");

        var token = await IdentityManager.GetAccessToken(CancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CommandException("You must be signed into Meadow.Cloud to execute this command. Run 'meadow cloud login' to do so.");
        }

        try
        {
            var response = await ApiTokenService.GetApiTokens(Host, CancellationToken);
            var apiTokens = response.OrderBy(a => a.Name);

            if (!apiTokens.Any())
            {
                Logger?.LogInformation("You have no API keys.");
                return;
            }

            var table = new ConsoleTable("Id", "Name", $"Expires (UTC)", "Scopes");
            foreach (var apiToken in apiTokens)
            {
                table.AddRow(apiToken.Id, apiToken.Name, $"{apiToken.ExpiresAt:G}", string.Join(", ", apiToken.Scopes.OrderBy(t => t)));
            }

            Logger?.LogInformation(table);
        }
        catch (MeadowCloudAuthException ex)
        {
            throw new CommandException("You must be signed in to execute this command.", innerException: ex);
        }
        catch (MeadowCloudException ex)
        {
            throw new CommandException($"Get API keys command failed: {ex.Message}", innerException: ex);
        }
    }
}


