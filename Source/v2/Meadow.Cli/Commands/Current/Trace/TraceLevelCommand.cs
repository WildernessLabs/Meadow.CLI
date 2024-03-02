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
        var device = await GetCurrentDevice();

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        if (Level <= 0)
        {
            Logger?.LogInformation($"{DisablingTracing}...");

            await device.SetTraceLevel(Level, CancellationToken);
        }
        else
        {
            Logger?.LogInformation($"Setting trace level to {Level}...");
            await connection.Device.SetTraceLevel(Level, CancellationToken);

            Logger?.LogInformation($"{Strings.EnablingTracing}...");
            await connection.Device.TraceEnable(CancellationToken);
        }
    }
}