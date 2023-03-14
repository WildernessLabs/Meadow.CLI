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
using Microsoft.Extensions.Configuration;

namespace Meadow.CLI.Core.CloudServices
{
    public class UserService : CloudServiceBase
    {
        IConfiguration _config;

        public UserService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
        }

        public async Task<List<UserOrg>> GetUserOrgs(CancellationToken cancellationToken)
        {
            var httpClient = await AuthenticatedHttpClient();

            var response = await httpClient.GetAsync($"{_config["meadowCloudHost"]}/api/users/me/orgs");

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
