using Meadow.CLI.Core.CloudServices.Messages;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public class UserService : CloudServiceBase
    {
        readonly IConfiguration _config;

        public UserService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
        }

        public async Task<List<UserOrg>> GetUserOrgs(string host, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

            var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

            var response = await httpClient.GetAsync($"{host}/api/users/me/orgs", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<UserOrg>>(message) ?? new List<UserOrg>();
            }
            else
            {
                return new List<UserOrg>();
            }
        }

        public async Task<User?> GetMe(string host, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

            var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

            var response = await httpClient.GetAsync($"{host}/api/users/me");

            if (response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<User>(message, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                return null;
            }
        }
    }
}