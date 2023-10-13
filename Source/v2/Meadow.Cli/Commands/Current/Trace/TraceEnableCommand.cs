using System.Reflection.Emit;
using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace enable", Description = "Enable trace logging on the Meadow")]
public class TraceEnableCommand : BaseTraceEnableCommand<TraceEnableCommand>
{
    [CommandOption("level", 'l', Description = "The desired trace level", IsRequired = false)]
    public int? Level { get; init; }

    public TraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                if (Level != null)
                {
                    Logger?.LogInformation($"Setting trace level to {Level}...");
                    await Connection.Device.SetTraceLevel(Level.Value, CancellationToken);
                }

                Logger?.LogInformation("Enabling tracing...");

                await Connection.Device.TraceEnable(CancellationToken);
            }
            else
            {
                Logger?.LogError("Trace Error: No Device found...");
            }
        }
    }
}