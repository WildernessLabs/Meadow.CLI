using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("trace enable", Description = "Enable trace logging on the Meadow")]
public class TraceEnableCommand : BaseDeviceCommand<TraceEnableCommand>
{
    [CommandOption("level", 'l', Description = "The desired trace level", IsRequired = false)]
    public int? Level { get; init; }

    public TraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger.LogInformation(e.message);
        };

        if (Level != null)
        {
            Logger.LogInformation($"Setting trace level to {Level}...");
            await device.SetTraceLevel(Level.Value, cancellationToken);
        }

        Logger.LogInformation("Enabling tracing...");

        await device.TraceEnable(cancellationToken);
    }
}

