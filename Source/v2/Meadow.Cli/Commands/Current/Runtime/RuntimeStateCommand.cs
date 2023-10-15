using CliFx.Attributes;
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
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                var state = await Connection.Device.IsRuntimeEnabled(CancellationToken);

                Logger?.LogInformation($"Runtime is {(state ? "ENABLED" : "DISABLED")}");
            }
        }
    }
}