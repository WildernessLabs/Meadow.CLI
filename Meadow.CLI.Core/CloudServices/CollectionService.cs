using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.CloudServices.Messages;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;

namespace Meadow.CLI.Core.CloudServices;

public class CollectionService : CloudServiceBase
{
    IConfiguration _config;
    IdentityManager _identityManager;

    public CollectionService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
    {
        _config = config;
        _identityManager = identityManager;
    }
    
    public async Task<List<Collection>> GetOrgCollections(string orgId, string host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(host))
        {
            host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
        }

        var authToken = await _identityManager.GetAccessToken(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(authToken))
        {
            throw new MeadowCloudAuthException();
        }

        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        var result = await httpClient.GetStringAsync($"{host}/api/orgs/{orgId}/collections");
        return JsonSerializer.Deserialize<List<Collection>>(result);
    }
}