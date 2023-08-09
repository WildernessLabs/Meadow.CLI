﻿using CliFx;
using CliFx.Infrastructure;
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

    protected abstract ValueTask ExecuteCommand(Hcom.IMeadowDevice device, CancellationToken cancellationToken);

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var c = ConnectionManager.GetCurrentConnection();

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
                    await ExecuteCommand(c.Device, cancellationToken);
                }
            }
            catch (TimeoutException)
            {
                Logger.LogError($"Timeout attempting to attach to device on {c.Name}");
            }
        }
    }
}
