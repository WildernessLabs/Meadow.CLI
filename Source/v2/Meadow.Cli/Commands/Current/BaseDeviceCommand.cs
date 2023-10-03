using CliFx;
using CliFx.Infrastructure;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public abstract class BaseDeviceCommand<T> : ICommand
{
    protected ILogger<T> Logger { get; }
    protected MeadowConnectionManager ConnectionManager { get; }

    public BaseDeviceCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger<T>();
        ConnectionManager = connectionManager;
    }

    protected abstract ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken);

    public virtual async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        IMeadowConnection? c = null;
        try
        {
            c = ConnectionManager.GetCurrentConnection();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed: {ex.Message}");
            return;
        }

        if (c != null)
        {
            c.ConnectionError += (s, e) =>
            {
                Logger.LogError(e.Message);
            };

            try
            {
                await c.Attach(cancellationToken);


                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogInformation($"Cancelled");
                    return;
                }

                if (c.Device == null)
                {
                    Logger.LogError("No device found");
                }
                else
                {
                    await ExecuteCommand(c, c.Device, cancellationToken);
                    Logger.LogInformation($"Done.");
                }
            }
            catch (TimeoutException)
            {
                Logger.LogError($"Timeout attempting to attach to device on {c.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed: {ex.Message}");
            }
        }
    }
}
