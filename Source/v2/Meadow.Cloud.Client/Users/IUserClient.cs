namespace Meadow.Cloud.Client.Users;

public interface IUserClient
{
    Task<IEnumerable<UserOrg>> GetOrgs(string hostName);
}
