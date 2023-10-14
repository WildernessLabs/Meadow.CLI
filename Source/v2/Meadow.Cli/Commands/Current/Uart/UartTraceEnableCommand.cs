using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart trace enable", Description = "Enables trace log output to UART")]
public class UartTraceEnableCommand : BaseTraceCommand<UartTraceEnableCommand>
{
    public UartTraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        Logger?.LogInformation("Setting UART to output trace messages...");

        if (Connection != null && Connection.Device != null)
            await Connection.Device.UartTraceEnable(CancellationToken);
    }
}