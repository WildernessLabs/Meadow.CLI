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

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger.LogInformation(e.message);
        };

        if (Level <= 0)
        {
            Logger.LogInformation("Disabling tracing...");

            await device.SetTraceLevel(Level, cancellationToken);
        }
        else
        {
            Logger.LogInformation($"Setting trace level to {Level}...");
            await device.SetTraceLevel(Level, cancellationToken);

            Logger.LogInformation("Enabling tracing...");
            await device.TraceEnable(cancellationToken);
        }
    }
}

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

