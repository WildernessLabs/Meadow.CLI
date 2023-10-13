using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseTraceEnableCommand<T> : BaseDeviceCommand<T>
{
    protected IMeadowConnection? Connection { get; private set; }

    public BaseTraceEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        Connection = await GetCurrentConnection();

        if (Connection != null)
        {
            Connection.DeviceMessageReceived += (s, e) =>
            {
                Logger?.LogInformation(e.message);
            };
        }
    }
}

