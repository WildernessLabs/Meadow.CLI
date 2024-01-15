using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime state", Description = "Gets the device's current runtime state")]
public class RuntimeStateCommand : BaseDeviceCommand<RuntimeStateCommand>
{
    public RuntimeStateCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            Logger?.LogError($"Runtime state failed - device or connection not found");
            return;
        }

        Logger?.LogInformation($"Querying runtime state...");

        var state = await connection.Device.IsRuntimeEnabled(CancellationToken);

        Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
    }
}