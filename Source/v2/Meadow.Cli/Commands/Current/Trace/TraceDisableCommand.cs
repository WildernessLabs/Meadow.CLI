using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace disable", Description = "Disable trace logging on the Meadow")]
public class TraceDisableCommand : BaseTraceEnableCommand<TraceDisableCommand>
{
    public TraceDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null && Connection.Device != null)
        {
            Logger?.LogInformation("Disabling tracing...");

            await Connection.Device.TraceDisable(CancellationToken);
        }
    }
}