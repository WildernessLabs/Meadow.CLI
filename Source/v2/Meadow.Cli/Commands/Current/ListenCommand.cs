using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("listen", Description = "Listen for console output from Meadow")]
public class ListenCommand : BaseDeviceCommand<ListenCommand>
{
    [CommandOption("no-prefix", 'n', IsRequired = false, Description = "When set, the message source prefix (e.g. 'stdout>') is suppressed")]
    public bool NoPrefix { get; set; }

    public ListenCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        var connection = connectionManager.GetCurrentConnection();

        if (connection == null)
        {
            Logger.LogError($"No device connection configured.");
            return;
        }

        Logger.LogInformation($"Listening for Meadow Console output on '{connection.Name}'. Press Ctrl+C to exit...");

        connection.DeviceMessageReceived += OnDeviceMessageReceived;
    }

    private void OnDeviceMessageReceived(object? sender, (string message, string? source) e)
    {
        if (NoPrefix)
        {
            Logger.LogInformation($"{e.message.TrimEnd('\n', '\r')}");
        }
        else
        {
            Logger.LogInformation($"{e.source}> {e.message.TrimEnd('\n', '\r')}");
        }
    }

    protected override async ValueTask ExecuteCommand(Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }

        Logger.LogInformation($"Cancelled.");
    }
}
