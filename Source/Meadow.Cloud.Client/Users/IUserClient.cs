namespace Meadow.Cloud.Client.Users;

public interface IUserClient
{
    Task<IEnumerable<GetOrganizationResponse>> GetOrganizations(CancellationToken cancellationToken = default);
    Task<GetUserResponse?> GetUser(CancellationToken cancellationToken = default);
}
