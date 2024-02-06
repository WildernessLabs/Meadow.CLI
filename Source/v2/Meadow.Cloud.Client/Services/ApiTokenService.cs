namespace Meadow.Cloud.Client;

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
            throw new MeadowCloudClassicException(message);
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
            throw new MeadowCloudClassicException(message);
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
            throw new MeadowCloudClassicException(message);
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
            throw new MeadowCloudClassicException(message);
        }
    }
}

public class GetApiTokenResponse(string id, string name, DateTimeOffset expiresAt, string[] scopes)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
    public string[] Scopes { get; set; } = scopes;
}

public class CreateApiTokenRequest(string name, int duration, string[] scopes)
{
    public string Name { get; set; } = name;
    public int Duration { get; set; } = duration;
    public string[] Scopes { get; set; } = scopes;
}

public class CreateApiTokenResponse(string id, string name, DateTimeOffset expiresAt, string[] scopes, string token)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
    public string[] Scopes { get; set; } = scopes;
    public string Token { get; set; } = token;
}

public class UpdateApiTokenRequest(string name, string[] scopes)
{
    public string Name { get; set; } = name;
    public string[] Scopes { get; set; } = scopes;
}

public class UpdateApiTokenResponse(string id, string name, DateTimeOffset expiresAt, string[] scopes)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
    public string[] Scopes { get; set; } = scopes;
}
