using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("mono disable", Description = "** deprecated **")]
public class MonoDisableCommand : RuntimeDisableCommand
{
    public MonoDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger.LogWarning($"Deprecated command.  Use `runtime disable` instead");
    }
}
