using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;

namespace Meadow.CLI.Core.CloudServices
{
    public class DeviceService : CloudServiceBase
    {
        IConfiguration _config;

        public DeviceService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
        }

        public async Task<(bool isSuccess, string message)> AddDevice(string orgId, string id, string publicKey, string collectionId)
        {
            var httpClient = await AuthenticatedHttpClient();

            dynamic payload = new
            {
                orgId,
                id,
                publicKey,
                collectionId
            };

            var json = JsonSerializer.Serialize<dynamic>(payload);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response  = await httpClient.PostAsync($"{_config["meadowCloudHost"]}/api/devices", content);

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
