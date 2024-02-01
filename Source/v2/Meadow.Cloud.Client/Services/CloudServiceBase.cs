using Meadow.Cloud.Client.Identity;
using System.Net.Http.Headers;

namespace Meadow.Cloud.Client;

public abstract class CloudServiceBase
{
    private readonly IdentityManager _identityManager;

    protected CloudServiceBase(IdentityManager identityManager)
    {
        _identityManager = identityManager;
    }

    protected async Task<HttpClient> GetAuthenticatedHttpClient(CancellationToken? cancellationToken = null)
    {
        var authToken = await _identityManager.GetAccessToken(cancellationToken ?? CancellationToken.None);
        if (string.IsNullOrEmpty(authToken))
        {
            throw new MeadowCloudAuthException();
        }

        HttpClient client = new();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        return client;
    }
}
