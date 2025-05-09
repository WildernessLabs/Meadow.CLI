﻿using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("developer", Description = "Sets a specified developer parameter on the Meadow")]
public class DeveloperCommand : BaseDeviceCommand<DeveloperCommand>
{
    [CommandOption("param", 'p', Description = "The parameter to set.", IsRequired = false)]
    public ushort Parameter { get; init; }

    [CommandOption("value", 'v', Description = "The value to apply to the parameter. Valid values are 0 to 4,294,967,295.", IsRequired = false)]
    public uint Value { get; init; }

    [CommandOption("timeout", 't', Description = "The Test timeout value. Default is 60 seconds.", IsRequired = false)]
    public uint TimeoutInSeconds { get; init; } = 60;

    public DeveloperCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();
        var device = await GetCurrentDevice();

        Logger?.LogInformation($"Setting developer parameter {Parameter} to {Value}");

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        await device.SetDeveloperParameter(Parameter, Value, TimeSpan.FromSeconds(TimeoutInSeconds), CancellationToken);
    }
}