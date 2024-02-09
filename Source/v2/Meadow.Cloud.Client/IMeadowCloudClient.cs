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

    Task<bool> Authenticate(string? host = default, CancellationToken cancellationToken = default);
}
