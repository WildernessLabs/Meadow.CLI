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
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            Logger?.LogError($"Runtime disable failed - device or connection not found");
            return;
        }

        Logger?.LogInformation($"Disabling runtime...");

        await connection.Device.RuntimeDisable(CancellationToken);

        var state = await connection.Device.IsRuntimeEnabled(CancellationToken);

        Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
    }
}