using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseDeviceCommand<T> : BaseCommand<T>
{
    protected IMeadowConnection? CurrentConnection { get; private set; }
    protected MeadowConnectionManager ConnectionManager { get; }

    public BaseDeviceCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        ConnectionManager = connectionManager;
    }

    protected async Task RefreshConnection()
    {
        CurrentConnection = ConnectionManager.GetCurrentConnection();

        if (CurrentConnection != null)
        {
            CurrentConnection.ConnectionError += (s, e) =>
            {
                Logger?.LogError(e.Message);
            };

            try
            {
                await CurrentConnection.Attach(CancellationToken);

                if (CancellationToken.IsCancellationRequested)
                {
                    Logger?.LogInformation($"Cancelled");
                    return;
                }

                if (CurrentConnection.Device == null)
                {
                    Logger?.LogError("No device found");
                }
                else
                {
                    Logger?.LogInformation($"Done.");
                }
            }
            catch (TimeoutException)
            {
                Logger?.LogError($"Timeout attempting to attach to device on {CurrentConnection?.Name}");
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
    }

    protected override async Task BeforeExecute()
    {
        await RefreshConnection();
    }
}