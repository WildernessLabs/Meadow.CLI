namespace Meadow.Cloud.Client.Devices;

public interface IDeviceClient
{
    Task<AddDeviceResponse> AddDevice(AddDeviceRequest request, CancellationToken cancellationToken = default);
}
