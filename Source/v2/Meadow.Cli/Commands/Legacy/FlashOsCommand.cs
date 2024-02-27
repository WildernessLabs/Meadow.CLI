using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("flash os", Description = "** Deprecated ** Use `firmware write` instead")]
public class FlashOsCommand : BaseDeviceCommand<FlashOsCommand>
{
    public FlashOsCommand(
            MeadowConnectionManager connectionManager,
            ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override ValueTask ExecuteCommand()
    {
        throw new CommandException($"Deprecated command. Use `firmware write` instead");
    }
}