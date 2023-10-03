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

[Command("cloud collection list", Description = "List Meadow Collections")]
public class CloudCollectionListCommand : BaseCloudCommand<CloudCollectionListCommand>
{
    public const string DefaultHost = "https://www.meadowcloud.co";

    [CommandOption("host", 'h', Description = $"Optionally set a host (default is {DefaultHost})", IsRequired = false)]
    public string? Host { get; set; }
    [CommandOption("orgId", 'o', Description = "Organization Id", IsRequired = false)]
    public string? OrgId { get; set; }

    public CloudCollectionListCommand(
        IdentityManager identityManager,
        UserService userService,
        DeviceService deviceService,
        CollectionService collectionService,
        ILoggerFactory? loggerFactory)
        : base(identityManager, userService, deviceService, collectionService, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(CancellationToken? cancellationToken)
    {
        UserOrg? org;

        try
        {
            if (Host == null) Host = DefaultHost;

            Logger?.LogInformation("Retrieving your user and organization information...");

            var userOrgs = await UserService.GetUserOrgs(Host, cancellationToken).ConfigureAwait(false);
            if (!userOrgs.Any())
            {
                Logger?.LogInformation($"Please visit {Host} to register your account.");
                return;
            }
            else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
            {
                Logger?.LogInformation($"You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.");
                return;
            }
            else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
            {
                OrgId = userOrgs.First().Id;
            }

            org = userOrgs.FirstOrDefault(o => o.Id == OrgId || o.Name == OrgId);
            if (org == null)
            {
                Logger?.LogInformation($"Unable to find an organization with a Name or ID matching '{OrgId}'");
                return;
            }
        }
        catch (MeadowCloudAuthException)
        {
            Logger?.LogError($"You must be signed in to execute this command.");
            Logger?.LogError($"Please run \"meadow cloud login\" to sign in to Meadow.Cloud.");
            return;
        }

        var collections = await CollectionService.GetOrgCollections(org.Id, Host, cancellationToken);

        if (collections == null || collections.Count == 0)
        {
            Logger?.LogInformation("No collections found.");
        }
        else
        {
            Logger?.LogInformation("Collections:");
            foreach (var collection in collections)
            {
                Logger?.LogInformation($" {collection.Id} | {collection.Name}");
            }
        }
    }
}
