using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime enable", Description = "Sets the runtime to run on the Meadow board then resets it")]
public class RuntimeEnableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public RuntimeEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger?.LogInformation($"Enabling runtime...");
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                await Connection.Device.RuntimeEnable(CancellationToken);

                var state = await Connection.Device.IsRuntimeEnabled(CancellationToken);

                Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
            }
        }
    }
}