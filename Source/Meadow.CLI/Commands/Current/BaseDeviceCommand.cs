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

    protected Task<IMeadowConnection> GetCurrentConnection(bool forceReconnect = false)
        => GetConnection(null, forceReconnect);

    protected Task<IMeadowConnection> GetConnectionForRoute(string route, bool forceReconnect = false)
        => GetConnection(route, forceReconnect);

    private async Task<IMeadowConnection> GetConnection(string? route, bool forceReconnect = false)
    {
        IMeadowConnection? connection = null;

        if (route != null)
        {
            connection = ConnectionManager.GetConnectionForRoute(route, forceReconnect);
        }
        else
        {
            connection = ConnectionManager.GetCurrentConnection(forceReconnect);
        }

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
                throw new CommandException($"Timeout attempting to attach to device on {connection?.Name}", CommandExitCode.ConnectionNotFound);
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