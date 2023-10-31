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
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                Logger?.LogInformation($"Erasing flash...");

                await Connection.Device.EraseFlash(CancellationToken);
            }
        }
    }
}