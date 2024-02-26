namespace Meadow.Cloud.Client.Devices;

public class DeviceClient : IDeviceClient
{
    private readonly MeadowCloudClient _meadowCloudClient;
    private readonly DeviceService _deviceService;

    internal DeviceClient(MeadowCloudClient meadowCloudClient, IdentityManager identityManager)
    {
        _deviceService = new DeviceService(identityManager);
        _meadowCloudClient = meadowCloudClient;
    }

    public async Task Provision(string hostName, string deviceID, string devicePublicKey, string? deviceName, UserOrg? org)
    {
        if (!await _meadowCloudClient!.Authenticate())
        {
            throw new NotAuthenticatedException();
        }

        if (org == null)
        {
            org = (await _meadowCloudClient
                .User
                .GetOrgs(hostName))
                .First();
        }

        var result = await _deviceService.AddDevice(
            org.Id,
            deviceID!,
            devicePublicKey,
            org.DefaultCollectionId,
            deviceName,
            hostName);

        if (!result.isSuccess)
        {
            throw new Exception(result.message);
        }
    }
}
