using Meadow.CLI.Core.Auth;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public class DeviceRepository
    {
        public async Task<bool> AddDevice(string serialNumber)
        {
            var host = SettingsManager.GetAppSetting("wlApiHost");
            var authToken = await new IdentityManager().GetAccessToken();
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            dynamic payload = new 
            {
                serialNumber = serialNumber
            };

            var json = JsonSerializer.Serialize<dynamic>(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response  = await client.PostAsync($"{host}/devices", content);

            return response.IsSuccessStatusCode;
        }
    }
}
