﻿using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("listen", Description = "Listen for console output from Meadow")]
public class ListenCommand : BaseDeviceCommand<ListenCommand>
{
    [CommandOption("prefix", 'p', Description = "When set, the message source prefix (e.g. 'stdout>') is shown", IsRequired = false)]
    public bool Prefix { get; init; } = false;

    public ListenCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    private void Connection_ConnectionMessage(object? sender, string e)
    {
        //ToDo
    }

    private void OnDeviceMessageReceived(object? sender, (string message, string? source) e)
    {
        if (Prefix)
        {
            Logger?.LogInformation($"{e.source}> {e.message.TrimEnd('\n', '\r')}");
        }
        else
        {
            Logger?.LogInformation($"{e.message.TrimEnd('\n', '\r')}");
        }
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        connection.DeviceMessageReceived += OnDeviceMessageReceived;
        connection.ConnectionMessage += Connection_ConnectionMessage;

        Logger?.LogInformation($"Listening for Meadow Console output on '{connection.Name}'. Press Ctrl+C to exit...");

        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }
    }
}