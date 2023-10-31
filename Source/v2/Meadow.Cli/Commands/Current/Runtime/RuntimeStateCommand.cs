using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime state", Description = "Gets the device's current runtime state")]
public class RuntimeStateCommand : BaseDeviceCommand<RuntimeStateCommand>
{
    public RuntimeStateCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
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
                try
                {
                    Logger?.LogInformation($"Querying runtime state...");

                    await Connection.Device.IsRuntimeEnabled(CancellationToken);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"Unable to determine the runtime state.");
                }
            }
        }
    }
}