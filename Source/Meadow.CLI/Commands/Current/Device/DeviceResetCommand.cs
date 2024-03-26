using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device reset", Description = "Resets the device")]
public class DeviceResetCommand : BaseDeviceCommand<DeviceResetCommand>
{
    public DeviceResetCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogInformation($"Resetting the device...");
    }

    protected override async ValueTask ExecuteCommand()
    {
        await (await GetCurrentDevice()).Reset();
    }
}