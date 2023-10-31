using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device reset", Description = "Resets the device")]
public class DeviceResetCommand : BaseDeviceCommand<DeviceResetCommand>
{
    public DeviceResetCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null && Connection.Device != null)
        {
            Logger?.LogInformation($"Resetting the device...");
            await Connection.Device.Reset();
        }
    }
}
