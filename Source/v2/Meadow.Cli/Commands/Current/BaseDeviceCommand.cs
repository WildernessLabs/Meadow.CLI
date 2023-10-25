using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseDeviceCommand<T> : BaseCommand<T>
{
    protected MeadowConnectionManager ConnectionManager { get; }
    public IMeadowConnection? Connection { get; private set; }

    public BaseDeviceCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        ConnectionManager = connectionManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        Connection = await GetCurrentConnection();
    }

    protected async Task<IMeadowConnection?> GetCurrentConnection()
    {
        var connection = ConnectionManager.GetCurrentConnection();

        if (connection != null)
        {
            AttachMessageHandlers(connection);

            try
            {
                await connection.Attach(CancellationToken);

                if (CancellationToken.IsCancellationRequested)
                {
                    Logger?.LogInformation($"Cancelled");
                    return null;
                }

                if (connection.Device == null)
                {
                    Logger?.LogError("No device found");
                }

                return connection;
            }
            catch (TimeoutException)
            {
                Logger?.LogError($"Timeout attempting to attach to device on {connection?.Name}");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Failed: {ex.Message}");
            }
        }
        else
        {
            Logger?.LogError("Current Connnection Unavailable"); // No connection path is defined ??
        }

        return null;
    }

    private void AttachMessageHandlers(IMeadowConnection? connection)
    {
        if (connection != null)
        {
            connection.ConnectionError += Connection_ConnectionError;

            connection.ConnectionMessage += Connection_ConnectionMessage;

            // the connection passes messages back to us (info about actions happening on-device)
            connection.DeviceMessageReceived += Connection_DeviceMessageReceived;
        }
    }

    private void Connection_DeviceMessageReceived(object? sender, (string message, string? source) e)
    {
        if (e.message.Contains("% downloaded"))
        {
            // don't echo this, as we're already reporting % written
        }
        else
        {
            Logger?.LogInformation(e.message);
        }
    }

    private void Connection_ConnectionMessage(object? sender, string message)
    {
        Logger?.LogInformation(message);
    }

    private void Connection_ConnectionError(object? sender, Exception e)
    {
        Logger?.LogError(e.Message);
    }

    public void DetachMessageHandlers(IMeadowConnection? connection)
    {
        if (connection != null)
        {
            connection.ConnectionError -= Connection_ConnectionError;

            connection.ConnectionMessage -= Connection_ConnectionMessage;

            // the connection passes messages back to us (info about actions happening on-device)
            connection.DeviceMessageReceived -= Connection_DeviceMessageReceived;
        }
    }
}