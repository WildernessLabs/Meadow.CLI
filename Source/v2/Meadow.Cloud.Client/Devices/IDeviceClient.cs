namespace Meadow.Cloud.Client.Devices;

public interface IDeviceClient
{
    Task Provision(string hostName, string deviceID, string devicePublicKey, string? deviceName, UserOrg? org);
}
