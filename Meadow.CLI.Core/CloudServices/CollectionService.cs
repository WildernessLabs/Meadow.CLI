using Meadow.CLI.Core.CloudServices.Messages;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices;

public class CollectionService : CloudServiceBase
{
    readonly IConfiguration _config;

    public CollectionService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
    {
        _config = config;
    }
    
    public async Task<List<Collection>> GetOrgCollections(string orgId, string host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(host))
        {
            host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
        }

        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var result = await httpClient.GetStringAsync($"{host}/api/orgs/{orgId}/collections");
        return JsonSerializer.Deserialize<List<Collection>>(result) ?? new List<Collection>();
    }
}