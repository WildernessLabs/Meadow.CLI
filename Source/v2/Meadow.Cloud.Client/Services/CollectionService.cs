namespace Meadow.Cloud.Client;

public class CollectionService : CloudServiceBase
{
    public CollectionService(IdentityManager identityManager) : base(identityManager)
    {
    }

    public async Task<List<Collection>> GetOrgCollections(string orgId, string host, CancellationToken? cancellationToken)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var result = await httpClient.GetStringAsync($"{host}/api/orgs/{orgId}/collections");
        return JsonSerializer.Deserialize<List<Collection>>(result) ?? new List<Collection>();
    }
}
