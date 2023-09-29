using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("mono disable", Description = "** Deprecated ** Use `runtime disable` instead ")]
public class MonoDisableCommand : RuntimeDisableCommand
{
    public MonoDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger.LogWarning($"Deprecated command.  Use `runtime disable` instead");
    }
}
