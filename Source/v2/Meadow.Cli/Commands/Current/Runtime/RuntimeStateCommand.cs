using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime state", Description = "Gets the device's current runtime state")]
public class RuntimeStateCommand : BaseDeviceCommand<RuntimeStateCommand>
{
    public RuntimeStateCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogInformation($"Querying runtime state...");
    }

    protected override async ValueTask ExecuteCommand()
    {
        var state = await CurrentConnection.Device.IsRuntimeEnabled(CancellationToken);

        Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
    }
}
