using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace enable", Description = "Enable trace logging on the Meadow")]
public class TraceEnableCommand : BaseDeviceCommand<TraceEnableCommand>
{
    [CommandOption("level", 'l', Description = "The desired trace level", IsRequired = false)]
    public int? Level { get; init; }

    public TraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            Logger?.LogError($"Trace enable failed - device or connection not found");
            return;
        }

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        if (Level != null)
        {
            Logger?.LogInformation($"Setting trace level to {Level}...");
            await connection.Device.SetTraceLevel(Level.Value, CancellationToken);
        }

        Logger?.LogInformation("Enabling tracing...");

        await connection.Device.TraceEnable(CancellationToken);
    }
}