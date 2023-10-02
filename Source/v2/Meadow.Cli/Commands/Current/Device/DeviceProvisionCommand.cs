using CliFx.Attributes;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Meadow.Hcom;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device provision", Description = "Registers and prepares connected device for use with Meadow Cloud")]
public class DeviceProvisionCommand : BaseDeviceCommand<DeviceProvisionCommand>
{
    private IConfiguration? _config;

    [CommandOption("orgId", 'o', Description = "The target org for device registration", IsRequired = false)]
    public string OrgId { get; set; }
    [CommandOption("collectionId", 'c', Description = "The target collection for device registration", IsRequired = false)]
    public string CollectionId { get; set; }
    [CommandOption("name", 'n', Description = "Device friendly name", IsRequired = false)]
    public string Name { get; set; }
    [CommandOption("host", 'h', Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string Host { get; set; }

    public DeviceProvisionCommand(IConfiguration? config, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        try
        {
            var identityManager = new IdentityManager(Logger);
            var _userService = new UserService(identityManager);

            var userOrgs = await _userService.GetUserOrgs(Host, cancellationToken).ConfigureAwait(false);
            if (!userOrgs.Any())
            {
                Logger.LogInformation($"Please visit {_config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME]} to register your account.");
                return;
            }
            else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
            {
                Logger.LogInformation($"Please specify the orgId for this device provisioning.");
                return;
            }
            else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
            {
                OrgId = userOrgs.First().Id;
            }

            if (!userOrgs.Select(x => x.Id).Contains(OrgId))
            {
                Logger.LogInformation($"Invalid orgId: {OrgId}");
                return;
            }
        }
        catch (MeadowCloudAuthException)
        {
            Logger.LogError($"You must be signed in to execute this command.");
            Logger.LogError($"Please run \"meadow cloud login\" to sign in to Meadow.Cloud.");
            return;
        }

        Logger.LogInformation("Requesting device public key...");
        var publicKey = await device.GetPublicKey(cancellationToken);

        var delim = "-----END PUBLIC KEY-----\n";
        publicKey = publicKey.Substring(0, publicKey.IndexOf(delim) + delim.Length);



        /*
        var result = await _deviceService.AddDevice(OrgId, device.DeviceInfo.ProcessorId, publicKey, CollectionId, Name, Host);

        if (result.isSuccess)
        {
            Logger.LogInformation("Device provisioned successfully");
        }
        else
        {
            Logger.LogInformation(result.message);
        }
        */

        return;
    }
}
