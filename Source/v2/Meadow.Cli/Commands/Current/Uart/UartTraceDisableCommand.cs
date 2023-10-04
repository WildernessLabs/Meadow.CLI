using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart trace disable", Description = "Disables trace log output to UART")]
public class UartTraceDisableCommand : BaseDeviceCommand<UartTraceDisableCommand>
{
    public UartTraceDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        CurrentConnection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        Logger?.LogInformation("Setting UART to application use...");

        await CurrentConnection.Device.UartTraceDisable(CancellationToken);
    }
}
