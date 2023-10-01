using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace level", Description = "Sets the trace logging level on the Meadow")]
public class TraceLevelCommand : BaseDeviceCommand<TraceLevelCommand>
{
    [CommandParameter(0, Name = "Level", IsRequired = true)]
    public int Level { get; set; }

    public TraceLevelCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        CurrentConnection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        if (Level <= 0)
        {
            Logger?.LogInformation("Disabling tracing...");

            await CurrentConnection.Device.SetTraceLevel(Level, CancellationToken);
        }
        else
        {
            Logger?.LogInformation($"Setting trace level to {Level}...");
            await CurrentConnection.Device.SetTraceLevel(Level, CancellationToken);

            Logger?.LogInformation("Enabling tracing...");
            await CurrentConnection.Device.TraceEnable(CancellationToken);
        }
    }
}
