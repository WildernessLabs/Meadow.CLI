using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Meadow.CLI.Core.Identity;
using Meadow.CLI.Core.Logging;

namespace Meadow.CLI.Core.CloudServices
{
    public class DeviceRepository
    {
        private readonly IMeadowLogger _logger;

        public DeviceRepository(IMeadowLogger logger)
        {
            _logger = logger;
        }

        public async Task<(bool isSuccess, string message)> AddDevice(string serialNumber)
        {
            var host = SettingsManager.GetAppSetting("wlApiHost");
            var authToken = await new IdentityManager(_logger).GetAccessToken();
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            dynamic payload = new 
            {
                serialNumber = serialNumber
            };

            var json = JsonSerializer.Serialize<dynamic>(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response  = await client.PostAsync($"{host}/devices", content);

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
