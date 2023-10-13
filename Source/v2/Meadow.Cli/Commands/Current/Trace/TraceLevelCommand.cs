using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace level", Description = "Sets the trace logging level on the Meadow")]
public class TraceLevelCommand : BaseTraceEnableCommand<TraceLevelCommand>
{
    [CommandParameter(0, Name = "Level", IsRequired = true)]
    public int Level { get; set; }

    public TraceLevelCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null && Connection.Device != null)
        {
            if (Level <= 0)
            {
                Logger?.LogInformation("Disabling tracing...");

                await Connection.Device.TraceDisable(CancellationToken);
            }
            else
            {
                Logger?.LogInformation($"Setting trace level to {Level}...");
                await Connection.Device.SetTraceLevel(Level, CancellationToken);

                Logger?.LogInformation("Enabling tracing...");
                await Connection.Device.TraceEnable(CancellationToken);
            }
        }
    }
}