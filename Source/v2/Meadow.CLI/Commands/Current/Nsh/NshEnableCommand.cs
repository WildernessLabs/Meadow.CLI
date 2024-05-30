using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("nsh enable", Description = "Enables the Nuttx shell on the Meadow device")]
public class NshEnableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public NshEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation("Enabling NSH...");

        var state = await device.IsRuntimeEnabled(CancellationToken);

        await device.NshEnable(CancellationToken);

        Logger?.LogInformation("NSH enabled");
    }
}