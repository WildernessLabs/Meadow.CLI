using System;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.Hardware;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("port list", Description = "List available local serial ports")]
public class PortListCommand : BaseCommand<PortListCommand>
{
    public PortListCommand(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IConsole console, CancellationToken cancellationToken)
    {
        var portlist = await MeadowConnectionManager.GetSerialPorts();
        for (int i = 0; i < portlist.Count; i++)
        {
            Logger?.LogInformation($"{i + 1}: {portlist[i]}");
        }
        Logger?.LogInformation($"{Environment.NewLine}Type the number of the port you would like to use.{Environment.NewLine}or just press Enter to keep your current port.");

        byte deviceSelected;
        if (byte.TryParse(await console.Input.ReadLineAsync(), out deviceSelected))
        {
            if (deviceSelected > 0 && deviceSelected  <= portlist.Count)
            {
                var setCommand = new ConfigCommand(new SettingsManager(), LoggerFactory)
                {
                    Settings = new string[] { "route", portlist[deviceSelected - 1] }
                };

                await setCommand.ExecuteAsync(console);
            }
        }
    }
}