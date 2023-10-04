using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace disable", Description = "Disable trace logging on the Meadow")]
public class TraceDisableCommand : BaseDeviceCommand<TraceDisableCommand>
{
    public TraceDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null)
        {
            return;
        }

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        Logger?.LogInformation("Disabling tracing...");

        await connection.Device.TraceDisable(CancellationToken);
    }
}

