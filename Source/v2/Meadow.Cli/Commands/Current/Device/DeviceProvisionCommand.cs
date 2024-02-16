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

    public DeviceProvisionCommand(UserService userService, DeviceService deviceService, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _deviceService = deviceService;
        _userService = userService;
    }

    protected override async ValueTask ExecuteCommand()
    {
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

        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            throw CommandException.MeadowDeviceNotFound;
        }

        var info = await connection.Device.GetDeviceInfo(CancellationToken);

        Logger?.LogInformation(Strings.RequestingDevicePublicKey);
        var publicKey = await connection.Device.GetPublicKey(CancellationToken);

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new CommandException(
                Strings.CouldNotRetrievePublicKey,
                CommandExitCode.GeneralError);
        }

        var delimiters = new string[]
        {
            "-----END PUBLIC KEY-----\n", // F7 delimiter
            "-----END RSA PUBLIC KEY-----\n" // linux/mac/windows delimiter
        };

        var valid = false;

        foreach (var delim in delimiters)
        {
            var index = publicKey.IndexOf(delim);
            if (index > 0)
            {
                valid = true;
                publicKey = publicKey.Substring(0, publicKey.IndexOf(delim) + delim.Length);
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
        var provisioningID = !string.IsNullOrWhiteSpace(info?.ProcessorId) ? info.ProcessorId : info?.SerialNumber;
        var provisioningName = !string.IsNullOrWhiteSpace(Name) ? Name : info?.DeviceName;

        var result = await _deviceService.AddDevice(org.Id!, provisioningID!, publicKey, CollectionId, provisioningName, Host, CancellationToken);

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