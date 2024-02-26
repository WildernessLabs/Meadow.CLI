namespace Meadow.Cloud.Client.Users;

public class UserClient : IUserClient
{
    private UserService _userService;

    internal UserClient(IdentityManager identityManager)
    {
        _userService = new UserService(identityManager);
    }

    public async Task<IEnumerable<UserOrg>> GetOrgs(string hostName)
    {
        return await _userService.GetUserOrgs(hostName);
    }
}
