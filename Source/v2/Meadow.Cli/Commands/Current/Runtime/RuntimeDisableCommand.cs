using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime disable", Description = "Sets the runtime to NOT run on the Meadow board then resets it")]
public class RuntimeDisableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public RuntimeDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                Logger?.LogInformation($"Disabling runtime...");

                await Connection.Device.RuntimeDisable(CancellationToken);

                var state = await Connection.Device.IsRuntimeEnabled(CancellationToken);

                Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
            }
        }
    }
}