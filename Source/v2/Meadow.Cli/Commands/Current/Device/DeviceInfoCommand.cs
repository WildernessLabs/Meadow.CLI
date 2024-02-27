using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device info", Description = "Get the device info")]
public class DeviceInfoCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    public DeviceInfoCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogInformation(Strings.GettingDeviceInfo);
    }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        var deviceInfo = await device.GetDeviceInfo(CancellationToken);

        if (deviceInfo != null)
        {
            Logger?.LogInformation(deviceInfo.ToString());
        }
    }
}