using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart trace enable", Description = "Enables trace log output to UART")]
public class UartTraceEnableCommand : BaseDeviceCommand<UartTraceEnableCommand>
{
    public UartTraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            Logger?.LogError($"Uart trace enable failed - device or connection not found");
            return;
        }

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        Logger?.LogInformation("Setting UART to output trace messages...");

        await connection.Device.UartTraceEnable(CancellationToken);
    }
}