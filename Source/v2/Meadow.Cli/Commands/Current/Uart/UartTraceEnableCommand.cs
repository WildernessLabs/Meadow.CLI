using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart trace enable", Description = "Enables trace log output to UART")]
public class UartTraceEnableCommand : BaseDeviceCommand<UartTraceEnableCommand>
{
    public UartTraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger.LogInformation(e.message);
        };

        Logger.LogInformation("Setting UART to output trace messages...");

        await device.UartTraceEnable(cancellationToken);
    }
}
