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
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            throw CommandException.MeadowDeviceNotFound;
        }

        var deviceInfo = await connection.Device.GetDeviceInfo(CancellationToken);
        if (deviceInfo != null)
        {
            Logger?.LogInformation(deviceInfo.ToString());
        }
    }
}