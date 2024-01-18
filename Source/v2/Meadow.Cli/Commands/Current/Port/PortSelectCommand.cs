using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("port select", Description = "Select a port from a list of available local serial ports")]
public class PortSelectCommand : BaseCommand<PortSelectCommand>
{
    public PortSelectCommand(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        if (LoggerFactory != null && Console != null)
        {
            var portListCommand = new PortListCommand(LoggerFactory);

            await portListCommand.ExecuteAsync(Console);

            if (portListCommand.Portlist?.Count > 0)
            {
                if (portListCommand.Portlist?.Count > 1)
                {
                    Logger?.LogInformation($"{Environment.NewLine}Type the number of the port you would like to use.{Environment.NewLine}or just press Enter to keep your current port.");

                    byte deviceSelected;
                    if (byte.TryParse(await Console.Input.ReadLineAsync(), out deviceSelected))
                    {
                        if (deviceSelected > 0 && deviceSelected <= portListCommand.Portlist?.Count)
                        {
                            await CallConfigCommand(portListCommand.Portlist[deviceSelected - 1]);
                        }
                    }
                }
                else
                {
                    // Only 1 device attached, let's auto select it
                    if (portListCommand.Portlist != null)
                        await CallConfigCommand(portListCommand.Portlist[0]);
                }
            }
        }
    }

    private async Task CallConfigCommand(string selectedPort)
    {
        var setCommand = new ConfigCommand(new SettingsManager(), LoggerFactory)
        {
            Settings = new string[] { "route", selectedPort }
        };

        if (Console != null)
        {
            await setCommand.ExecuteAsync(Console);
        }
    }
}