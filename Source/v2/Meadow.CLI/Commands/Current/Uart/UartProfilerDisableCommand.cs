using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart profiler disable", Description = "Disables profiling data output to UART")]
public class UartProfilerDisableCommand : BaseDeviceCommand<UartProfilerDisableCommand>
{
    public UartProfilerDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            return;
        }

        Logger?.LogInformation("Disabling UART output for profiling data...");

        await connection.Device.UartProfilerDisable(CancellationToken);

        Logger?.LogInformation("Reseting Meadow device...");

        await connection.ResetDevice();
    }
}