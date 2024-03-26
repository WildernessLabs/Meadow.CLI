using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public class CommandService : CloudServiceBase
    {
        readonly IConfiguration _config;
        readonly IdentityManager _identityManager;

        public CommandService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
            _identityManager = identityManager;
        }

        public async Task PublishCommandForCollection(
            string collectionId,
            string commandName,
            JsonDocument? arguments = null,
            int qualityOfService = 0,
            string? host = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

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
                throw new MeadowCloudException(message);
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
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

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
                throw new MeadowCloudException(message);
            }
        }
    }
}
