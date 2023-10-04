using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime disable", Description = "Sets the runtime to NOT run on the Meadow board then resets it")]
public class RuntimeDisableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public RuntimeDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogInformation($"Disabling runtime...");
    }

    protected override async ValueTask ExecuteCommand()
    {
        await CurrentConnection.Device.RuntimeDisable(CancellationToken);

        var state = await CurrentConnection.Device.IsRuntimeEnabled(CancellationToken);

        Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
    }
}
