using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime disable", Description = "Sets the runtime to NOT run on the Meadow board then resets it")]
public class RuntimeDisableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public RuntimeDisableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation($"{Strings.DisablingRuntime}...");

        var state = await device.IsRuntimeEnabled(CancellationToken);

        if (state == false)
        {
            Logger?.LogInformation("Runtime already disabled");
        }
        else
        {
            await device.RuntimeDisable(CancellationToken);

            state = await device.IsRuntimeEnabled(CancellationToken);

            if (state == true)
            {
                Logger?.LogError("Failed to disable runtime");
            }

            Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
        }
    }
}