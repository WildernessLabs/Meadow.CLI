namespace Meadow.Cloud.Client;

public class ApiTokenService : CloudServiceBase
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public ApiTokenService(IMeadowCloudClient meadowCloudClient) : base(meadowCloudClient)
    {
    }

    public async Task<IEnumerable<GetApiTokenResponse>> GetApiTokens(string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var response = await httpClient.GetAsync($"{host}/api/auth/tokens", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(response.StatusCode, response: message);
        }

        return await response.Content.ReadFromJsonAsync<IEnumerable<GetApiTokenResponse>>(JsonSerializerOptions, cancellationToken)
            ?? Enumerable.Empty<GetApiTokenResponse>();
    }

    public async Task<CreateApiTokenResponse> CreateApiToken(CreateApiTokenRequest request, string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{host}/api/auth/tokens", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(response.StatusCode, response: message);
        }

        var result = await response.Content.ReadFromJsonAsync<CreateApiTokenResponse>(JsonSerializerOptions, cancellationToken);
        return result!;
    }

    public async Task<UpdateApiTokenResponse> UpdateApiToken(string id, UpdateApiTokenRequest request, string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");
        var response = await httpClient.PutAsync($"{host}/api/auth/tokens/{id}", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(response.StatusCode, response: message);
        }

        var result = await response.Content.ReadFromJsonAsync<UpdateApiTokenResponse>(JsonSerializerOptions, cancellationToken);
        return result!;
    }

    public async Task DeleteApiToken(string id, string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);
        var response = await httpClient.DeleteAsync($"{host}/api/auth/tokens/{id}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(response.StatusCode, response: message);
        }
    }
}

public class GetApiTokenResponse
{
    public GetApiTokenResponse(string id, string name, DateTimeOffset expiresAt, string[] scopes)
    {
        Id = id;
        Name = name;
        ExpiresAt = expiresAt;
        Scopes = scopes;
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string[] Scopes { get; set; }
}

public class CreateApiTokenRequest
{
    public CreateApiTokenRequest(string name, int duration, string[] scopes)
    {
        Name = name;
        Duration = duration;
        Scopes = scopes;
    }

    public string Name { get; set; }
    public int Duration { get; set; }
    public string[] Scopes { get; set; }
}

public class CreateApiTokenResponse
{
    public CreateApiTokenResponse(string id, string name, DateTimeOffset expiresAt, string[] scopes, string token)
    {
        Id = id;
        Name = name;
        ExpiresAt = expiresAt;
        Scopes = scopes;
        Token = token;
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string[] Scopes { get; set; }
    public string Token { get; set; }
}

public class UpdateApiTokenRequest
{
    public UpdateApiTokenRequest(string name, string[] scopes)
    {
        Name = name;
        Scopes = scopes;
    }

    public string Name { get; set; }
    public string[] Scopes { get; set; }
}

public class UpdateApiTokenResponse
{
    public UpdateApiTokenResponse(string id, string name, DateTimeOffset expiresAt, string[] scopes)
    {
        Id = id;
        Name = name;
        ExpiresAt = expiresAt;
        Scopes = scopes;
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string[] Scopes { get; set; }
}
