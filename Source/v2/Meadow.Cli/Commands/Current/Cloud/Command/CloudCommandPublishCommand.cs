using CliFx.Attributes;
using CliFx.Exceptions;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("cloud command publish", Description = "Publish a command to Meadow devices via the Meadow Service")]
public class CloudCommandPublishCommand : BaseCloudCommand<CloudCommandPublishCommand>
{
    public const string DefaultHost = "https://www.meadowcloud.co";

    [CommandParameter(0, Description = "The name of the command", IsRequired = true, Name = "COMMAND_NAME")]
    public string CommandName { get; set; }

    [CommandOption("collectionId", 'c', Description = "The target collection for publishing the command")]
    public string? CollectionId { get; set; }

    [CommandOption("deviceIds", 'd', Description = "The target devices for publishing the command")]
    public string[]? DeviceIds { get; set; }

    [CommandOption("args", 'a', Description = "The arguments for the command as a JSON string", Converter = typeof(JsonDocumentBindingConverter))]
    public JsonDocument? Arguments { get; set; }

    [CommandOption("qos", 'q', Description = "The MQTT-defined quality of service for the command")]
    public QualityOfService QualityOfService { get; set; } = QualityOfService.AtLeastOnce;

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)")]
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

    protected override async ValueTask ExecuteCommand(CancellationToken? cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CollectionId) && (DeviceIds == null || DeviceIds.Length == 0))
        {
            throw new CommandException("Either a collection ID (-c|--collectionId) or a list of device IDs (-d|--deviceIds) must be specified.", showHelp: true);
        }

        if (!string.IsNullOrWhiteSpace(CollectionId) && (DeviceIds != null && DeviceIds.Length > 0))
        {
            throw new CommandException("Cannot specify both a collection ID (-c|--collectionId) and list of device IDs (-d|--deviceIds). Only one is allowed.", showHelp: true);
        }

        if (Host == null) Host = DefaultHost;

        var token = await IdentityManager.GetAccessToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CommandException("You must be signed into Meadow.Cloud to execute this command. Run 'meadow cloud login' to do so.");
        }

        try
        {
            Logger?.LogInformation($"Publishing '{CommandName}' command to Meadow.Cloud. Please wait...");
            if (!string.IsNullOrWhiteSpace(CollectionId))
            {
                await CommandService.PublishCommandForCollection(CollectionId, CommandName, Arguments, (int)QualityOfService, Host, cancellationToken);
            }
            else if (DeviceIds.Any())
            {
                await CommandService.PublishCommandForDevices(DeviceIds, CommandName, Arguments, (int)QualityOfService, Host, cancellationToken);
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
