using Meadow.CLI.Core.CloudServices.DTO;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public class NotificationService
    {
        private readonly ILogger _logger;

        public NotificationService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<(bool isSuccess, string message)> SendNotification(string orgId, string message, string topic="")
        {
            var host = SettingsManager.GetAppSetting("wlApiHost");
            var authToken = await new IdentityManager(_logger).GetAccessToken();
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            dynamic payload = new
            {
                topic,
                message
            };

            var json = JsonSerializer.Serialize<dynamic>(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{host}/orgs/{orgId}/notifications", content);

            if (response.IsSuccessStatusCode)
            {
                return (response.IsSuccessStatusCode, string.Empty);
            }
            else
            {
                var reason = await response.Content.ReadAsStringAsync();
                return (false, reason);
            }

        }

        public async Task<string> GetUsersOrganizationId()
        {
            var host = SettingsManager.GetAppSetting("wlApiHost");
            var authToken = await new IdentityManager(_logger).GetAccessToken();
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var response = await client.GetAsync($"{host}/users/me");
            if (response.IsSuccessStatusCode)
            {
                var user = await JsonSerializer.DeserializeAsync<User>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                if (user != null)
                {
                    return user.OrgId;
                }
            }

            return string.Empty;
        }
    }
}
