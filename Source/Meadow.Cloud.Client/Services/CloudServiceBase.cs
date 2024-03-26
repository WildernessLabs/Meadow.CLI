namespace Meadow.Cloud.Client;

public abstract class CloudServiceBase
{
    private readonly IMeadowCloudClient _meadowCloudClient;

    protected CloudServiceBase(IMeadowCloudClient meadowCloudClient)
    {
        _meadowCloudClient = meadowCloudClient;
    }

    protected async Task<HttpClient> GetAuthenticatedHttpClient(CancellationToken cancellationToken = default)
    {
        if (_meadowCloudClient.Authorization == null)
        {
            var result = await _meadowCloudClient.Authenticate(cancellationToken);
            if (!result)
            {
                throw new MeadowCloudAuthException();
            }
        }

        return new HttpClient
        {
            DefaultRequestHeaders =
            {
                Authorization = _meadowCloudClient.Authorization
            }
        };
    }
}
