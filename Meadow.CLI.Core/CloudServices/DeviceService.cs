using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;

namespace Meadow.CLI.Core.CloudServices
{
    public class DeviceService
    {
        private readonly ILogger _logger;

        public DeviceService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<(bool isSuccess, string message)> AddDevice(string orgId, string serialNumber, string publicKey)
        {
            var host = SettingsManager.GetAppSetting("wlApiHost");
            var authToken = await new IdentityManager(_logger).GetAccessToken();
            if (string.IsNullOrEmpty(authToken))
            {
                throw new MeadowCloudAuthException();
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            dynamic payload = new
            {
                orgId,
                serialNumber,
                publicKey
            };

            var json = JsonSerializer.Serialize<dynamic>(payload);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response  = await client.PostAsync($"{host}/api/devices", content);

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
