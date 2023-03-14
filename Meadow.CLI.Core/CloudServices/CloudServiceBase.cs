using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public abstract class CloudServiceBase
    {
        IdentityManager _identityManager;

        protected CloudServiceBase(IdentityManager identityManager)
        {
            _identityManager = identityManager;
        }

        protected async Task<HttpClient> AuthenticatedHttpClient()
        {
            var authToken = await _identityManager.GetAccessToken();
            if (string.IsNullOrEmpty(authToken))
            {
                throw new MeadowCloudAuthException();
            }

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            return client;
        }
    }
}
