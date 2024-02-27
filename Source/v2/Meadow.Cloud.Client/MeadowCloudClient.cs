using Meadow.Cloud.Client.ApiTokens;
using Meadow.Cloud.Client.Collections;
using Meadow.Cloud.Client.Commands;
using Meadow.Cloud.Client.Devices;
using Meadow.Cloud.Client.Firmware;
using Meadow.Cloud.Client.Packages;
using Meadow.Cloud.Client.Users;

namespace Meadow.Cloud.Client;

public class MeadowCloudClient : IMeadowCloudClient
{
    public const string DefaultHost = "https://www.meadowcloud.co";
    public static readonly Uri DefaultHostUri = new(DefaultHost);

    private readonly Lazy<FirmwareClient> _firmwareClient;
    private readonly Lazy<UserClient> _userClient;
    private readonly MeadowCloudContext _meadowCloudContext;
    private readonly IdentityManager _identityManager;

    public MeadowCloudClient(HttpClient httpClient, IdentityManager identityManager, MeadowCloudUserAgent userAgent, ILoggerFactory? loggerFactory = default)
    {
        loggerFactory ??= NullLoggerFactory.Instance;

        _meadowCloudContext = new(httpClient, userAgent);
 
        _firmwareClient = new Lazy<FirmwareClient>(() => new FirmwareClient(_meadowCloudContext, loggerFactory.CreateLogger<FirmwareClient>()));
        _userClient = new Lazy<UserClient>(() => new UserClient(_meadowCloudContext, loggerFactory.CreateLogger<UserClient>()));
        _identityManager = identityManager;
    }

    public IApiTokenClient ApiToken => throw new NotImplementedException("This client is not implemented yet. Please use the 'ApiTokenService' instead.");
    public ICollectionClient Collection => throw new NotImplementedException("This client is not implemented yet. Please use the 'CollectionService' instead.");
    public ICommandClient Command => throw new NotImplementedException("This client is not implemented yet. Please use the 'CommandService' instead.");
    public IDeviceClient Device => throw new NotImplementedException("This client is not implemented yet. Please use the 'DeviceService' instead.");
    public IFirmwareClient Firmware => _firmwareClient.Value;
    public IPackageClient Package => throw new NotImplementedException("This client is not implemented yet. Please use the 'PackageService' instead.");
    public IUserClient User => _userClient.Value;

    public async Task<bool> Authenticate(CancellationToken cancellationToken = default)
    {
        var token = await _identityManager.GetAccessToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        Authorization = new AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public AuthenticationHeaderValue? Authorization
    {
        get => _meadowCloudContext.Authorization;
        set => _meadowCloudContext.Authorization = value;
    }

    public Uri BaseAddress
    {
        get => _meadowCloudContext.BaseAddress;
        set => _meadowCloudContext.BaseAddress = value;
    }

    public MeadowCloudUserAgent UserAgent
    {
        get => _meadowCloudContext.UserAgent;
        set => _meadowCloudContext.UserAgent = value;
    }
}
