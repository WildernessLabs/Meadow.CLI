using CliFx.Attributes;
using Meadow.Cloud;
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

            Logger?.LogInformation("Retrieving your user and organization information...");

            var userOrgs = await _userService.GetUserOrgs(Host, CancellationToken).ConfigureAwait(false);

            if (userOrgs == null || !userOrgs.Any())
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

        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            return;
        }

        var info = await connection.Device.GetDeviceInfo(CancellationToken);

        Logger?.LogInformation("Requesting device public key (this will take a minute)...");
        var publicKey = await connection.Device.GetPublicKey(CancellationToken);

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            Logger?.LogError("Could not retrieve device's public key.");
            return;
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
            Logger?.LogError("Device returned an invali dpublic key");
            return;
        }

        Logger?.LogInformation("Provisioning device with Meadow.Cloud...");
        var provisioningID = !string.IsNullOrWhiteSpace(info?.ProcessorId) ? info.ProcessorId : info?.SerialNumber;
        var provisioningName = !string.IsNullOrWhiteSpace(Name) ? Name : info?.DeviceName;

        var result = await _deviceService.AddDevice(org.Id!, provisioningID!, publicKey, CollectionId, provisioningName, Host, CancellationToken);

        if (result.isSuccess)
        {
            Logger?.LogInformation("Device provisioned successfully");
        }
        else
        {
            Logger?.LogError($"Failed to provision device: {result.message}");
        }
    }
}