using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart trace disable", Description = "Disables trace log output to UART")]
public class UartTraceDisableCommand : BaseTraceCommand<UartTraceDisableCommand>
{
    public UartTraceDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        Logger?.LogInformation("Setting UART to application use...");

        if (Connection != null && Connection.Device != null)
            await Connection.Device.UartTraceDisable(CancellationToken);
    }
}
