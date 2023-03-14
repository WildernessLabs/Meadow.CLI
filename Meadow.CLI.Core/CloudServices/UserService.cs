using Meadow.CLI.Core.CloudServices.Messages;
using Meadow.CLI.Core.Identity;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Net;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;

namespace Meadow.CLI.Core.CloudServices
{
    public class UserService
    {
        private readonly ILogger _logger;

        public UserService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<List<UserOrg>> GetUserOrgs(CancellationToken cancellationToken)
        {
            var host = SettingsManager.GetAppSetting("wlApiHost");
            var identityManager = new IdentityManager(_logger);
            var authToken = await identityManager.GetAccessToken(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(authToken))
            {
                throw new MeadowCloudAuthException();
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var response = await client.GetAsync($"{host}/api/users/me/orgs");

            if (response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<UserOrg>>(message);
            }
            else
            {
                return new List<UserOrg>();
            }
        }
    }
}
