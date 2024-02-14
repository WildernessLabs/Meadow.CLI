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

    private readonly Lazy<FirmwareClient> _firmwareClient;
    private readonly HttpClient _httpClient;
    private readonly IdentityManager _identityManager;
    private readonly ILogger _logger;

    public MeadowCloudClient(HttpClient httpClient, IdentityManager identityManager, MeadowCloudUserAgent userAgent, ILogger<MeadowCloudClient>? logger = default)
    {
        _firmwareClient = new Lazy<FirmwareClient>(() => new FirmwareClient(httpClient));

        if (httpClient.BaseAddress == null)
        {
            httpClient.BaseAddress = new Uri(DefaultHost);
        }

        _httpClient = httpClient;
        _identityManager = identityManager;
        _logger = logger ?? NullLogger<MeadowCloudClient>.Instance;

        _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    public IApiTokenClient ApiToken => throw new NotImplementedException("This client is not implemented yet. Please use the 'ApiTokenService' instead.");
    public ICollectionClient Collection => throw new NotImplementedException("This client is not implemented yet. Please use the 'CollectionService' instead.");
    public ICommandClient Command => throw new NotImplementedException("This client is not implemented yet. Please use the 'CommandService' instead.");
    public IDeviceClient Device => throw new NotImplementedException("This client is not implemented yet. Please use the 'DeviceService' instead.");
    public IFirmwareClient Firmware => _firmwareClient.Value;
    public IPackageClient Package => throw new NotImplementedException("This client is not implemented yet. Please use the 'PackageService' instead.");
    public IUserClient User => throw new NotImplementedException("This client is not implemented yet. Please use the 'UserService' instead.");

    public async Task<bool> Authenticate(string? host = default, CancellationToken cancellationToken = default)
    {
        host ??= DefaultHost;

        _logger.LogInformation($"Authenticating with Meadow.Cloud{(host != DefaultHost ? $" ({host.ToLowerInvariant()})" : string.Empty)}...");

        var token = await _identityManager.GetAccessToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(host);
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return true;
    }
}
