namespace Meadow.Cloud.Client.Users;

public class UserClient : MeadowCloudClientBase, IUserClient
{
    public UserClient(MeadowCloudContext meadowCloudContext, ILogger logger)
        : base(meadowCloudContext, logger)
    {
    }

    public async Task<IEnumerable<GetOrganizationResponse>> GetOrganizations(CancellationToken cancellationToken = default)
    {
        using var request = CreateHttpRequestMessage(HttpMethod.Get, "api/v1/users/me/orgs");
        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Enumerable.Empty<GetOrganizationResponse>();
        }

        return await ProcessResponse<IEnumerable<GetOrganizationResponse>>(response, cancellationToken);
    }

    public async Task<GetUserResponse?> GetUser(CancellationToken cancellationToken = default)
    {
        using var request = CreateHttpRequestMessage(HttpMethod.Get, "api/v1/users/me");
        using var response = await HttpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ProcessResponse<GetUserResponse>(response, cancellationToken);
    }
}