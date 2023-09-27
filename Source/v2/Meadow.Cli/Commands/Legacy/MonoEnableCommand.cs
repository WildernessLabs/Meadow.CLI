using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("mono enable", Description = "** deprecated **")]
public class MonoEnableCommand : RuntimeEnableCommand
{
    public MonoEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger.LogWarning($"Deprecated command.  Use `runtime enable` instead");
    }
}