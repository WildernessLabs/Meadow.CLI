using CliFx.Attributes;
using Meadow.Cloud.Client;
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

    private ApiTokenService ApiTokenService { get; }

    public CloudApiKeyCreateCommand(
        IMeadowCloudClient meadowCloudClient,
        ApiTokenService apiTokenService,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        ApiTokenService = apiTokenService;
    }

    protected override ValueTask PreAuthenticatedValidation()
    {
        if (Duration < 1 || Duration > 90)
        {
            throw new CommandException("Duration (-d|--duration) must be between 1 and 90 days.", showHelp: true);
        }

        Logger.LogInformation($"Creating an API key on Meadow.Cloud{(Host != DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");
        return ValueTask.CompletedTask;
    }

    protected async override ValueTask ExecuteCloudCommand()
    {
        try
        {
            var request = new CreateApiTokenRequest(Name, Duration, Scopes);
            var response = await ApiTokenService.CreateApiToken(request, Host, CancellationToken);

            Logger.LogInformation($"Your API key '{response.Name}' (expiring {response.ExpiresAt:G} UTC) is:");
            Logger.LogInformation($"\n{response.Token}\n");
            Logger.LogInformation("Make sure to copy this key now as you will not be able to see this again.");
        }
        catch (MeadowCloudException ex)
        {
            throw new CommandException($"Create API key command failed: {ex.Message}", innerException: ex);
        }
    }
}
