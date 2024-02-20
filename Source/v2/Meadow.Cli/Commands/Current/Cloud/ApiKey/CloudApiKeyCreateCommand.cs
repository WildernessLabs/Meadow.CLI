using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud apikey create", Description = "Create a Meadow.Cloud API key")]
public class CloudApiKeyCreateCommand : BaseCloudCommand<CloudApiKeyCreateCommand>
{
    [CommandParameter(0, Description = "The name of the API key", IsRequired = true, Name = "NAME")]
    public string Name { get; init; } = default!;

    [CommandOption("duration", 'd', Description = "The duration of the API key, in days", IsRequired = true)]
    public int Duration { get; init; } = default!;

    [CommandOption("scopes", 's', Description = "The list of scopes (permissions) to grant the API key", IsRequired = true)]
    public string[] Scopes { get; init; } = default!;

    [CommandOption("host", Description = $"Optionally set a host (default is {DefaultHost})", IsRequired = false)]
    public string? Host { get; set; }

    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyCreateCommand(
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
        if (Duration < 1 || Duration > 90)
        {
            throw new CommandException("Duration (-d|--duration) must be between 1 and 90 days.", showHelp: true);
        }

        Host ??= DefaultHost;

        Logger?.LogInformation($"Creating an API key on Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");

        var token = await IdentityManager.GetAccessToken(CancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CommandException("You must be signed into your Wilderness Labs account to execute this command. Run 'meadow cloud login' to do so.");
        }

        try
        {
            var request = new CreateApiTokenRequest(Name!, Duration, Scopes!);
            var response = await ApiTokenService.CreateApiToken(request, Host, CancellationToken);

            Logger?.LogInformation($"Your API key '{response.Name}' (expiring {response.ExpiresAt:G} UTC) is:");
            Logger?.LogInformation($"\n{response.Token}\n");
            Logger?.LogInformation("Make sure to copy this key now as you will not be able to see this again.");
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
