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
            connection.ConnectionError += (s, e) =>
            {
                Logger?.LogError(e.Message);
            };

            connection.ConnectionMessage += (s, message) =>
            {
                Logger?.LogInformation(message);
            };

            // the connection passes messages back to us (info about actions happening on-device)
            connection.DeviceMessageReceived += (s, e) =>
            {
                if (e.message.Contains("% downloaded"))
                {
                    // don't echo this, as we're already reporting % written
                }
                else
                {
                    Logger?.LogInformation(e.message);
                }
            };

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
}