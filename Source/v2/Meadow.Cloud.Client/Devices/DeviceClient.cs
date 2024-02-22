using Microsoft.Extensions.Logging;

namespace Meadow.Cloud.Client.Devices;

public class DeviceClient : MeadowCloudClientBase, IDeviceClient
{
    public DeviceClient(MeadowCloudContext meadowCloudContext, ILogger logger)
        : base(meadowCloudContext, logger)
    {
    }

    public async Task<AddDeviceResponse> AddDevice(AddDeviceRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var httpRequest = CreateHttpRequestMessage(HttpMethod.Post, "api/v1/devices", request);
        using var httpResponse = await HttpClient.SendAsync(httpRequest, cancellationToken);

        return await ProcessResponse<AddDeviceResponse>(httpResponse, cancellationToken);
    }
}