using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("runtime enable", Description = "Sets the runtime to run on the Meadow board then resets it")]
public class RuntimeEnableCommand : BaseDeviceCommand<RuntimeEnableCommand>
{
    public RuntimeEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
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
                    Logger?.LogInformation($"Enabling runtime...");

                    await Connection.Device.RuntimeEnable(CancellationToken);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"Failed to enable runtime.");
                }
            }
        }
    }
}