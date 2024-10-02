using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app debug", Description = "Debug a running Meadow application")]
public class AppDebugCommand : BaseDeviceCommand<AppDebugCommand>
{
    // VS 2019 - 4024
    // VS 2017 - 4022
    // VS 2015 - 4020
    [CommandOption("Port", 'p', Description = "The port to run the debug server on", IsRequired = false)]
    public int Port { get; init; } = 4024;

    [CommandOption("Verbose", 'v', Description = "Enable verbose messages", IsRequired = false)]
    public bool Verbose { get; init; } = false;

    public AppDebugCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        if (Verbose)
        {
            connection.ConnectionMessage += (o, m) =>
            {
                Logger?.LogInformation(m);
            };

            connection.ConnectionError +=(o, e) =>
            {
                Logger?.LogInformation(e.Message);
            };

            connection.DebuggerMessageReceived += (s, e) =>
            {
                Logger?.LogInformation(e.ToString());
            };
        }

        using var server = await connection.StartDebuggingSession(Port, Logger, CancellationToken);

        if (Console != null)
        {
            Logger?.LogInformation("Debugging server started - press Enter to exit");
            await Console.Input.ReadLineAsync();
        }
    }
}