using CliFx.Attributes;
using Meadow.Cloud.Client;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device provision", Description = "Registers and prepares connected device for use with Meadow Cloud")]
public class DeviceProvisionCommand : BaseDeviceCommand<DeviceProvisionCommand>
{
    private readonly DeviceService _deviceService;
    private readonly UserService _userService;

    public const string DefaultHost = "https://www.meadowcloud.co";

    [CommandOption("orgId", 'o', Description = "The target org for device registration", IsRequired = false)]
    public string? OrgId { get; set; }

    [CommandOption("collectionId", 'c', Description = "The target collection for device registration", IsRequired = false)]
    public string? CollectionId { get; init; }

    [CommandOption("name", 'n', Description = "Device friendly name", IsRequired = false)]
    public string? Name { get; init; }

    [CommandOption("host", 'h', Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    [CommandOption("public-key", 'k', Description = "The public key of the device to provision.  If not provided, it will be queried from the configured device.", IsRequired = false)]
    public string? PublicKey { get; set; }

    [CommandOption("id", 'i', Description = "The unique ID/serial number of the device to provision.  If not provided, it will be queried from the configured device.", IsRequired = false)]
    public string? SerialNumber { get; set; }

    [CommandOption("gen-command", Description = "Generate a provisioning command for the configured device", IsRequired = false)]
    public bool GenerateProvisionCommand { get; set; }

    public DeviceProvisionCommand(UserService userService, DeviceService deviceService, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _deviceService = deviceService;
        _userService = userService;
    }

    private async ValueTask OutputProvisionCommand()
    {
        var device = await GetCurrentDevice();

        var info = await device.GetDeviceInfo(CancellationToken);

        Logger?.LogInformation(Strings.RequestingDevicePublicKey);

        var publicKey = await device.GetPublicKey(CancellationToken);
        publicKey = publicKey.Replace("\r", string.Empty).Replace("\n", string.Empty);
        var provisioningID = !string.IsNullOrWhiteSpace(info?.ProcessorId) ? info.ProcessorId : info?.SerialNumber;

        var command = "meadow device provision";
        if (!string.IsNullOrWhiteSpace(OrgId))
        {
            command += $" -o {OrgId}";
        }
        command += $" -id \"{provisioningID}\" -k \"{publicKey}\"";
        Logger?.LogInformation($"Provisioning command is:");
        Logger?.LogInformation(command);
    }

    protected override async ValueTask ExecuteCommand()
    {
        if (GenerateProvisionCommand)
        {
            Logger?.LogInformation("Generating a device provisioning command...");

            await OutputProvisionCommand();
            return;
        }

        UserOrg? org;

        try
        {
            Host ??= DefaultHost;

            Logger?.LogInformation(Strings.RetrievingUserAndOrgInfo);

            var userOrgs = await _userService.GetUserOrgs(Host, CancellationToken).ConfigureAwait(false);

            if (userOrgs == null || !userOrgs.Any())
            {
                throw new CommandException(
                    $"Please visit {Host} to register your account.",
                    CommandExitCode.InvalidParameter);
            }
            else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
            {
                throw new CommandException(
                    Strings.MemberOfMoreThanOneOrg,
                    CommandExitCode.InvalidParameter);
            }
            else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
            {
                OrgId = userOrgs.First().Id;
            }

            org = userOrgs.FirstOrDefault(o => o.Id == OrgId || o.Name == OrgId);

            if (org == null)
            {
                throw new CommandException(
                    string.Format(Strings.UnableToFindMatchingOrg, OrgId),
                    CommandExitCode.InvalidParameter);
            }
        }
        catch (MeadowCloudAuthException)
        {
            throw new CommandException(
                Strings.MustBeSignedInRunMeadowLogin,
                CommandExitCode.NotAuthorized);
        }

        string provisioningID, provisioningName;

        if (PublicKey != null)
        {
            if (SerialNumber == null)
            {
                throw new CommandException("If a public key is provided, an `id` must also be provided");
            }
            provisioningID = SerialNumber.ToUpper();
            provisioningName = Name ?? string.Empty;
        }
        else
        {
            var device = await GetCurrentDevice();

            var info = await device.GetDeviceInfo(CancellationToken);

            Logger?.LogInformation(Strings.RequestingDevicePublicKey);
            PublicKey = await device.GetPublicKey(CancellationToken);

            provisioningID = !string.IsNullOrWhiteSpace(info?.ProcessorId) ? info.ProcessorId : info?.SerialNumber;
            provisioningName = !string.IsNullOrWhiteSpace(Name) ? Name : info?.DeviceName;
        }

        if (string.IsNullOrWhiteSpace(PublicKey))
        {
            throw new CommandException(
                Strings.CouldNotRetrievePublicKey,
                CommandExitCode.GeneralError);
        }

        var delimiters = new string[]
        {
            "-----END PUBLIC KEY-----\n", // F7 delimiter
            "-----END PUBLIC KEY-----", // F7 delimiter
            "-----END RSA PUBLIC KEY-----\n", // linux/mac/windows delimiter
            "-----END RSA PUBLIC KEY-----" // linux/mac/windows delimiter
        };

        var valid = false;

        foreach (var delim in delimiters)
        {
            var index = PublicKey.IndexOf(delim);
            if (index > 0)
            {
                valid = true;
                PublicKey = PublicKey.Substring(0, PublicKey.IndexOf(delim) + delim.Length);
                break;
            }
        }

        if (!valid)
        {
            throw new CommandException(
                Strings.DeviceReturnedInvalidPublicKey,
                CommandExitCode.GeneralError);
        }

        Logger?.LogInformation(Strings.ProvisioningWithCloud);

        var result = await _deviceService.AddDevice(org.Id!, provisioningID!, PublicKey, CollectionId, provisioningName, Host, CancellationToken);

        if (result.isSuccess)
        {
            Logger?.LogInformation(Strings.ProvisioningSucceeded);
        }
        else
        {
            throw new CommandException(
                string.Format(Strings.ProvisioningFailed, result.message),
                CommandExitCode.GeneralError);
        }
    }
}