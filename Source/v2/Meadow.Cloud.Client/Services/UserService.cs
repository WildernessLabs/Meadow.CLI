namespace Meadow.Cloud.Client;

public class UserService : CloudServiceBase
{
    public UserService(IMeadowCloudClient meadowCloudClient) : base(meadowCloudClient)
    {
    }

    public async Task<List<UserOrg>> GetUserOrgs(string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var response = await httpClient.GetAsync($"{host}/api/users/me/orgs", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<UserOrg>>(message) ?? new List<UserOrg>();
        }
        else
        {
            return new List<UserOrg>();
        }
    }

    public async Task<User?> GetMe(string host, CancellationToken cancellationToken = default)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var response = await httpClient.GetAsync($"{host}/api/users/me");

        if (response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<User>(message, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
        }
        else
        {
            return null;
        }
    }
}
