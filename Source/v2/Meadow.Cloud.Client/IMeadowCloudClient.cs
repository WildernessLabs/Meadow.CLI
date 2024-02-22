using System.Net.Http.Headers;
using Meadow.Cloud.Client.ApiTokens;
using Meadow.Cloud.Client.Collections;
using Meadow.Cloud.Client.Commands;
using Meadow.Cloud.Client.Devices;
using Meadow.Cloud.Client.Firmware;
using Meadow.Cloud.Client.Packages;
using Meadow.Cloud.Client.Users;

namespace Meadow.Cloud.Client;

public interface IMeadowCloudClient
{
    IApiTokenClient ApiToken { get; }
    ICollectionClient Collection { get; }
    ICommandClient Command { get; }
    IDeviceClient Device { get; }
    IFirmwareClient Firmware { get; }
    IPackageClient Package { get; }
    IUserClient User { get; }

    Task<bool> Authenticate(CancellationToken cancellationToken = default);

    public AuthenticationHeaderValue? Authorization { get; set; }
    public Uri BaseAddress { get; set; }
    public MeadowCloudUserAgent UserAgent { get; set; }
}
