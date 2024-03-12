namespace Meadow.Cloud.Client;

public class DeviceService : CloudServiceBase
{
    public DeviceService(IMeadowCloudClient meadowCloudClient) : base(meadowCloudClient)
    {
    }

    public async Task<(bool isSuccess, string message)> AddDevice(string orgId, string id, string publicKey, string? collectionId, string? name, string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        dynamic payload = new
        {
            orgId,
            id,
            publicKey,
            collectionId,
            name
        };

        var json = JsonSerializer.Serialize<dynamic>(payload);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{host}/api/devices", content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return (response.IsSuccessStatusCode, string.Empty);
        }
        else
        {
            var message = await response.Content.ReadAsStringAsync();
            return (false, message);
        }
    }
}
