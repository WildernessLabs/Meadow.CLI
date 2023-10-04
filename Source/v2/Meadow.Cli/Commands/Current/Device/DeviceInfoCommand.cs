using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device info", Description = "Get the device info")]
public class DeviceInfoCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    public DeviceInfoCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogInformation($"Getting device info...");
    }

    protected override async ValueTask ExecuteCommand()
    {
        if (CurrentConnection != null)
        {
            var deviceInfo = await CurrentConnection.Device.GetDeviceInfo(CancellationToken);
            if (deviceInfo != null)
            {
                Logger?.LogInformation(deviceInfo.ToString());
            }
        }
    }
}
