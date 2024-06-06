using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("nsh disable", Description = "Disables the Nuttx shell on the Meadow device")]
public class NshDisableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public NshDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation("Disabling NSH...");

        var state = await device.IsRuntimeEnabled(CancellationToken);

        await device.NshDisable(CancellationToken);

        Logger?.LogInformation("NSH disabled");
    }
}