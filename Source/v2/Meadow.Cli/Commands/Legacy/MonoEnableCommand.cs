using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("mono enable", Description = "** Deprecated ** Use `runtime enable` instead")]
public class MonoEnableCommand : RuntimeEnableCommand
{
    public MonoEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command - use `runtime enable` instead");
    }
}