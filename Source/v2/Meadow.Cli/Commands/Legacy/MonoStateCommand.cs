using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("mono state", Description = "** Deprecated ** Use `runtime state` instead")]
public class MonoStateCommand : RuntimeStateCommand
{
    public MonoStateCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command - use `runtime state` instead");
    }
}