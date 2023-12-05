using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device provision", Description = "Registers and prepares connected device for use with Meadow Cloud")]
public class DeviceProvisionCommand : BaseDeviceCommand<DeviceProvisionCommand>
{
    private DeviceService _deviceService;

    public const string DefaultHost = "https://www.meadowcloud.co";

    [CommandOption("orgId", 'o', Description = "The target org for device registration", IsRequired = false)]
    public string? OrgId { get; set; }

    [CommandOption("collectionId", 'c', Description = "The target collection for device registration", IsRequired = false)]
    public string? CollectionId { get; set; }

    [CommandOption("name", 'n', Description = "Device friendly name", IsRequired = false)]
    public string? Name { get; set; }

    [CommandOption("host", 'h', Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    public DeviceProvisionCommand(DeviceService deviceService, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _deviceService = deviceService;
    }

    protected override async ValueTask ExecuteCommand()
    {
        UserOrg? org;
        try
        {
            if (Host == null) Host = DefaultHost;

            var identityManager = new IdentityManager(Logger);
            var _userService = new UserService(identityManager);

            Logger?.LogInformation("Retrieving your user and organization information...");

            var userOrgs = await _userService.GetUserOrgs(Host, CancellationToken).ConfigureAwait(false);
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

        var connection = await GetCurrentConnection();

        if (connection == null)
        {
            Logger?.LogError($"No connection path is defined");
            return;
        }

        var info = await connection.Device!.GetDeviceInfo(CancellationToken);

        Logger?.LogInformation("Requesting device public key (this will take a minute)...");
        var publicKey = await connection.Device.GetPublicKey(CancellationToken);

        var delim = "-----END RSA PUBLIC KEY-----\n";
        publicKey = publicKey.Substring(0, publicKey.IndexOf(delim) + delim.Length);


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

        return;
    }
}
