using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public abstract class CloudServiceBase
    {
        readonly IdentityManager _identityManager;

        protected CloudServiceBase(IdentityManager identityManager)
        {
            _identityManager = identityManager;
        }

        protected async Task<HttpClient> GetAuthenticatedHttpClient(CancellationToken cancellationToken = default)
        {
            var authToken = await _identityManager.GetAccessToken(cancellationToken);
            if (string.IsNullOrEmpty(authToken))
            {
                throw new MeadowCloudAuthException();
            }

            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            return client;
        }
    }
}
