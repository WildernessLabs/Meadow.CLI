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

    protected async Task<IMeadowDevice> GetCurrentDevice()
    {
        return (await GetCurrentConnection()).Device ?? throw CommandException.MeadowDeviceNotFound;
    }

    protected async Task<IMeadowConnection> GetCurrentConnection()
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
                return connection;
            }
            catch (TimeoutException)
            {
                throw new CommandException("Timeout attempting to attach to device on {connection?.Name}", CommandExitCode.ConnectionNotFound);
            }
            catch (Exception ex)
            {
                throw new CommandException($"No device found: {ex.Message}", CommandExitCode.ConnectionNotFound);
            }
        }
        else
        {
            throw new CommandException("Connection to Meadow unavailable", CommandExitCode.ConnectionNotFound);
        }
    }
}