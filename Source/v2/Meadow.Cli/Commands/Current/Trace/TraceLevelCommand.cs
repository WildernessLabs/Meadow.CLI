using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace level", Description = "Sets the trace logging level on the Meadow")]
public class TraceLevelCommand : BaseDeviceCommand<TraceLevelCommand>
{
    [CommandParameter(0, Name = "Level", IsRequired = true)]
    public int Level { get; init; }

    public TraceLevelCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            Logger?.LogError($"Trace level failed - device or connection not found");
            return;
        }

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        if (Level <= 0)
        {
            Logger?.LogInformation("Disabling tracing...");

            await connection.Device.SetTraceLevel(Level, CancellationToken);
        }
        else
        {
            Logger?.LogInformation($"Setting trace level to {Level}...");
            await connection.Device.SetTraceLevel(Level, CancellationToken);

            Logger?.LogInformation("Enabling tracing...");
            await connection.Device.TraceEnable(CancellationToken);
        }
    }
}