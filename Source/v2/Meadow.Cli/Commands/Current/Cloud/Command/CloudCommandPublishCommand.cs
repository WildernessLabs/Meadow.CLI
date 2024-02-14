using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud command publish", Description = "Publish a command to Meadow devices via Meadow.Cloud")]
public class CloudCommandPublishCommand : BaseCloudCommand<CloudCommandPublishCommand>
{
    [CommandParameter(0, Description = "The name of the command", IsRequired = true, Name = "COMMAND_NAME")]
    public string CommandName { get; init; } = default!;

    [CommandOption("collectionId", 'c', Description = "The target collection for publishing the command", IsRequired = false)]
    public string? CollectionId { get; init; }

    [CommandOption("deviceIds", 'd', Description = "The target devices for publishing the command", IsRequired = false)]
    public string[]? DeviceIds { get; init; }

    [CommandOption("args", 'a', Description = "The arguments for the command as a JSON string", Converter = typeof(JsonDocumentBindingConverter), IsRequired = false)]
    public JsonDocument? Arguments { get; init; }

    [CommandOption("qos", 'q', Description = "The MQTT-defined quality of service for the command", IsRequired = false)]
    public QualityOfService QualityOfService { get; init; } = QualityOfService.AtLeastOnce;

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    private CommandService CommandService { get; }

    public CloudCommandPublishCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        CommandService commandService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
        CommandService = commandService;
    }

    protected override async ValueTask ExecuteCommand()
    {
        if (string.IsNullOrWhiteSpace(CollectionId) && (DeviceIds == null || DeviceIds.Length == 0))
        {
            throw new CommandException("Either a collection ID (-c|--collectionId) or a list of device IDs (-d|--deviceIds) must be specified.", showHelp: true);
        }

        if (!string.IsNullOrWhiteSpace(CollectionId) && (DeviceIds != null && DeviceIds.Length > 0))
        {
            throw new CommandException("Cannot specify both a collection ID (-c|--collectionId) and list of device IDs (-d|--deviceIds). Only one is allowed.", showHelp: true);
        }

        Host ??= DefaultHost;

        var token = await IdentityManager.GetAccessToken(CancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CommandException("You must be signed into Meadow.Cloud to execute this command. Run 'meadow cloud login' to do so.");
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(CollectionId))
            {
                await CommandService.PublishCommandForCollection(CollectionId, CommandName, Arguments, (int)QualityOfService, Host, CancellationToken);
            }
            else if (DeviceIds?.Length > 0)
            {
                await CommandService.PublishCommandForDevices(DeviceIds, CommandName, Arguments, (int)QualityOfService, Host, CancellationToken);
            }
            else
            {
                throw new CommandException("Cannot specify both a collection ID (-c|--collectionId) and list of device IDs (-d|--deviceIds). Only one is allowed.");
            }
            Logger?.LogInformation("Publish command successful.");
        }
        catch (MeadowCloudAuthException ex)
        {
            throw new CommandException("You must be signed in to execute this command.", innerException: ex);
        }
        catch (MeadowCloudException ex)
        {
            throw new CommandException($"Publish command failed: {ex.Message}", innerException: ex);
        }
    }
}
