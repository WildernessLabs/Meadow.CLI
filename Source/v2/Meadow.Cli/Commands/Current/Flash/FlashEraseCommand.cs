using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("flash erase", Description = "Erase the contents of the device flash storage")]
public class FlashEraseCommand : BaseDeviceCommand<FlashEraseCommand>
{
    public FlashEraseCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            Logger?.LogInformation($"Flash erase failed - device or connection not found");
            return;
        }

        Logger?.LogInformation($"Erasing flash...");

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        await connection.Device.EraseFlash(CancellationToken);
    }
}