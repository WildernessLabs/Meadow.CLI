using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace disable", Description = "Disable trace logging on the Meadow")]
public class TraceDisableCommand : BaseDeviceCommand<TraceDisableCommand>
{
    public TraceDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger.LogInformation(e.message);
        };

        Logger.LogInformation("Disabling tracing...");

        await device.TraceDisable(cancellationToken);
    }
}

