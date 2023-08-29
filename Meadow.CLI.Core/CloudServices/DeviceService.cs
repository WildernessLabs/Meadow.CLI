using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public class DeviceService : CloudServiceBase
    {
        readonly IConfiguration _config;

        public DeviceService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
        }
        
        public async Task<(bool isSuccess, string message)> AddDevice(string orgId, string id, string publicKey, string collectionId, string name, string host, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }
            
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

            var response  = await httpClient.PostAsync($"{host}/api/devices", content, cancellationToken);
            
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
}
