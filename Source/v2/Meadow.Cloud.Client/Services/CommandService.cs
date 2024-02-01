using Meadow.Cloud.Client.Identity;
using System.Text;
using System.Text.Json;

namespace Meadow.Cloud.Client;

public class CommandService : CloudServiceBase
{
    public CommandService(IdentityManager identityManager) : base(identityManager)
    {
    }

    public async Task PublishCommandForCollection(
        string collectionId,
        string commandName,
        JsonDocument? arguments = null,
        int qualityOfService = 0,
        string? host = null,
        CancellationToken? cancellationToken = null)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var payload = new
        {
            commandName,
            args = arguments,
            qos = qualityOfService
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{host}/api/collections/{collectionId}/commands", content, cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudClassicException(message);
        }
    }

    public async Task PublishCommandForDevices(
        string[] deviceIds,
        string commandName,
        JsonDocument? arguments = null,
        int qualityOfService = 0,
        string? host = null,
        CancellationToken? cancellationToken = null)
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
        var response = await httpClient.PostAsync($"{host}/api/devices/commands", content, cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudClassicException(message);
        }
    }
}
