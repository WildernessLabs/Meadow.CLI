using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("listen", Description = "Listen for console output from Meadow")]
public class ListenCommand : BaseDeviceCommand<ListenCommand>
{
    [CommandOption("prefix", 'p', Description = "When set, the message source prefix (e.g. 'stdout>') is shown", IsRequired = false)]
    public bool Prefix { get; init; } = false;
    [CommandOption("log", 'l', Description = "A file name to store output to", IsRequired = false)]
    public string? SaveFile { get; init; } = null;
    [CommandOption("timestamps", 't', Description = "When set, the message will be prefixed with a UTC timestamp", IsRequired = false)]
    public bool AddTimestamps { get; init; } = false;

    public ListenCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    private void Connection_ConnectionMessage(object? sender, string e)
    {
        //ToDo
    }

    private void OnDeviceMessageReceived(object? sender, (string message, string? source) e)
    {
        string message;

        if (Prefix)
        {
            message = $"{e.source}> {e.message.TrimEnd('\n', '\r')}";
        }
        else
        {
            message = $"{e.message.TrimEnd('\n', '\r')}";
        }
        if (AddTimestamps)
        {
            message = $"[{DateTime.UtcNow:O}] {message}";
        }

        Logger?.LogInformation(message);
        if (SaveFile is not null)
        {
            try
            {
                System.IO.File.AppendAllText(SaveFile, message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[CLI] Error writing to file {SaveFile}: {ex.Message}");
            }
        }
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        connection.DeviceMessageReceived += OnDeviceMessageReceived;
        connection.ConnectionMessage += Connection_ConnectionMessage;

        // Enable automatic reconnection if the serial port disconnects
        if (connection is Hcom.SerialConnection serialConnection)
        {
            serialConnection.AggressiveReconnectEnabled = true;

            serialConnection.ConnectionStateChanged += (sender, oldState, newState) =>
            {
                if (newState == Hcom.ConnectionState.Disconnected)
                {
                    Logger?.LogWarning($"Device disconnected. Attempting to reconnect...");
                }
                else if (newState == Hcom.ConnectionState.Connected && oldState == Hcom.ConnectionState.Disconnected)
                {
                    Logger?.LogInformation($"Device reconnected successfully.");
                }
            };
        }

        Logger?.LogInformation($"Listening for Meadow Console output on '{connection.Name}'. Press Ctrl+C to exit...");

        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }
    }
}