﻿using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart trace disable", Description = "Disables trace log output to UART")]
public class UartTraceDisableCommand : BaseDeviceCommand<UartTraceDisableCommand>
{
    public UartTraceDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();
        var device = await GetCurrentDevice();

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        Logger?.LogInformation("Setting UART to application use...");

        await device.UartTraceDisable(CancellationToken);
    }
}