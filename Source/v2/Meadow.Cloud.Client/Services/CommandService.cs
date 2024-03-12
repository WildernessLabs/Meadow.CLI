namespace Meadow.Cloud.Client;

public class CommandService : CloudServiceBase
{
    public CommandService(IMeadowCloudClient meadowCloudClient) : base(meadowCloudClient)
    {
    }

    public async Task PublishCommandForCollection(
        string collectionId,
        string commandName,
        JsonDocument? arguments = null,
        int qualityOfService = 0,
        string? host = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var payload = new
        {
            commandName,
            args = arguments,
            qos = qualityOfService
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{host}/api/collections/{collectionId}/commands", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(response.StatusCode, response: message);
        }
    }

    public async Task PublishCommandForDevices(
        string[] deviceIds,
        string commandName,
        JsonDocument? arguments = null,
        int qualityOfService = 0,
        string? host = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var payload = new
        {
            deviceIds,
            commandName,
            args = arguments,
            qos = qualityOfService
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{host}/api/devices/commands", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(response.StatusCode, response: message);
        }
    }
}
