using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app debug", Description = "Debugs a running application")]
public class AppDebugCommand : BaseDeviceCommand<AppDebugCommand>
{
    // VS 2019 - 4024
    // VS 2017 - 4022
    // VS 2015 - 4020
    [CommandOption("Port", 'p', Description = "The port to run the debug server on")]
    public int Port { get; init; } = 4024;

    public AppDebugCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            using (var server = await Connection.StartDebuggingSession(Port, Logger, CancellationToken))
            {
                if (Console != null)
                {
                    Logger?.LogInformation("Debugging server started. Press Enter to exit");
                    await Console.Input.ReadLineAsync();
                }
            }
        }
    }
}