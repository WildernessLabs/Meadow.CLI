using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device info", Description = "Get the device info")]
public class DeviceInfoCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    public DeviceInfoCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null && Connection.Device != null)
        {
            Logger?.LogInformation($"Getting device info...");
            var deviceInfo = await Connection.Device.GetDeviceInfo(CancellationToken);
            if (deviceInfo != null)
            {
                Logger?.LogInformation(deviceInfo.ToString());
            }
        }
    }
}