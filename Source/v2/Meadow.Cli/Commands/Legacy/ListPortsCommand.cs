using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("list ports", Description = "** Deprecated ** Use `port list` instead")]
public class ListPortsCommand : PortListCommand
{
    public ListPortsCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command.  Use `port list` instead");
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        return base.ExecuteAsync(console);
    }
}

