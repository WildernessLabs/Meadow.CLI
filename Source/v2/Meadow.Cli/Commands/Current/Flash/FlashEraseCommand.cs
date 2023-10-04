using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("flash erase", Description = "Erases the device's flash storage")]
public class FlashEraseCommand : BaseDeviceCommand<FlashEraseCommand>
{
    public FlashEraseCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        Logger?.LogInformation($"Erasing flash...");

        CurrentConnection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        await CurrentConnection.Device.EraseFlash(CancellationToken);
    }
}
