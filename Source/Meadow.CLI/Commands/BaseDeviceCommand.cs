using Meadow.Hcom;
using Microsoft.Extensions.Logging;
using CliFx.Attributes;
using Meadow.CLI;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseDeviceCommand<T> : BaseCommand<T>
{
    [CommandOption('s', Description = Strings.MeadowSerialPort, IsRequired = false)]
    public string? SerialPort { get; protected set; }

    protected MeadowConnectionManager ConnectionManager { get; }

    public BaseDeviceCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        ConnectionManager = connectionManager;
    }

    protected async Task<IMeadowDevice> GetCurrentDevice()
    {
        return (await GetCurrentConnection()).Device ?? throw CommandException.MeadowDeviceNotFound;
    }

    internal async Task<IMeadowConnection> GetCurrentConnection(bool forceReconnect = false)
        => await GetConnection(SerialPort, forceReconnect);

    internal async Task<IMeadowConnection> GetConnectionForRoute(string route, bool forceReconnect = false)
        => await GetConnection(route, forceReconnect);

    IMeadowConnection? _connection = null;

    private async Task<IMeadowConnection> GetConnection(string? route, bool forceReconnect = false)
    {
        if(_connection != null)
        {
            return _connection;
        }

        IMeadowConnection? connection = null;

        connection = await ConnectionManager.GetConnection(route, forceReconnect);

        if (connection != null)
        {
            _connection = connection;

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