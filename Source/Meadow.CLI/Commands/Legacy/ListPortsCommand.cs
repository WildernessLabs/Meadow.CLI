using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("list ports", Description = "** Deprecated ** Use `port list` instead")]
public class ListPortsCommand : PortListCommand
{
    public ListPortsCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command - use `port list` instead");
    }

    protected override ValueTask ExecuteCommand()
    {
        return base.ExecuteCommand();
    }
}