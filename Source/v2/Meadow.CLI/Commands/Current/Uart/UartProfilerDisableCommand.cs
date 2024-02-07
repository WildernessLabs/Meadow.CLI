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

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        Logger?.LogInformation("Setting UART to application use...");

        await connection.Device.UartProfilerDisable(CancellationToken);
    }
}