﻿using Meadow.Cloud;
using Meadow.Cloud.Identity;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Meadow.Cloud;

public class ApiTokenService : CloudServiceBase
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public ApiTokenService(IdentityManager identityManager) : base(identityManager)
    {
    }

    public async Task<IEnumerable<GetApiTokenResponse>> GetApiTokens(string host, CancellationToken? cancellationToken)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var response = await httpClient.GetAsync($"{host}/api/auth/tokens", cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(message);
        }

        return await response.Content.ReadFromJsonAsync<IEnumerable<GetApiTokenResponse>>(JsonSerializerOptions, cancellationToken ?? CancellationToken.None)
            ?? Enumerable.Empty<GetApiTokenResponse>();
    }

    public async Task<CreateApiTokenResponse> CreateApiToken(CreateApiTokenRequest request, string host, CancellationToken? cancellationToken)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{host}/api/auth/tokens", content, cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(message);
        }

        var result = await response.Content.ReadFromJsonAsync<CreateApiTokenResponse>(JsonSerializerOptions, cancellationToken ?? CancellationToken.None);
        return result!;
    }

    public async Task<UpdateApiTokenResponse> UpdateApiToken(string id, UpdateApiTokenRequest request, string host, CancellationToken? cancellationToken)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");
        var response = await httpClient.PutAsync($"{host}/api/auth/tokens/{id}", content, cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(message);
        }

        var result = await response.Content.ReadFromJsonAsync<UpdateApiTokenResponse>(JsonSerializerOptions, cancellationToken ?? CancellationToken.None);
        return result!;
    }

    public async Task DeleteApiToken(string id, string host, CancellationToken? cancellationToken)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var response = await httpClient.DeleteAsync($"{host}/api/auth/tokens/{id}", cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(message);
        }
    }
}

public record GetApiTokenResponse(
    string Id,
    string Name,
    DateTimeOffset ExpiresAt,
    string[] Scopes
)
{ }

public record CreateApiTokenRequest
(
    string Name,
    int Duration,
    string[] Scopes
)
{ }

public record CreateApiTokenResponse
(
    string Id,
    string Name,
    DateTimeOffset ExpiresAt,
    string[] Scopes,
    string Token
)
{ }

public record UpdateApiTokenRequest
(
    string Name,
    string[] Scopes
)
{ }

public record UpdateApiTokenResponse
(
    string Id,
    string Name,
    DateTimeOffset ExpiresAt,
    string[] Scopes
)
{ }
