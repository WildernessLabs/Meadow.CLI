using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseDeviceCommand<T> : BaseCommand<T>
{
    protected MeadowConnectionManager ConnectionManager { get; }

    public BaseDeviceCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        ConnectionManager = connectionManager;
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
            Logger?.LogError("Current Connnection Unavailable");
        }

        return null;
    }
}