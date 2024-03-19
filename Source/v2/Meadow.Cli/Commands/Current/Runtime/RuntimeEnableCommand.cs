using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime enable", Description = "Sets the runtime to run on the Meadow board then resets it")]
public class RuntimeEnableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public RuntimeEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation($"{Strings.EnablingRuntime}...");

        var state = await device.IsRuntimeEnabled(CancellationToken);

        if (state == true)
        {
            Logger?.LogInformation("Runtime already enabled");
        }
        else
        {
            await device.RuntimeEnable(CancellationToken);

            state = await device.IsRuntimeEnabled(CancellationToken);

            if (state == false)
            {
                Logger?.LogError("Failed to enable runtime");
            }

            Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
        }
    }
}