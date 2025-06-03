using CliFx.Attributes;
using Meadow.Hcom;
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

    [CommandOption('s', Description = Strings.MeadowSerialPort, IsRequired = false)]
    public string? SerialPort { get; private set; }

    public AppDebugCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        IMeadowConnection? connection;
        if (SerialPort is not null)
        {
            connection = await GetConnectionForRoute(SerialPort);
        }
        else
        {
            connection = await GetCurrentConnection();
        }

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        using var server = await connection.StartDebuggingSession(Port, Logger, CancellationToken);

        if (Console != null)
        {
            Logger?.LogInformation("Debugging server started - press Enter to exit");
            await Console.Input.ReadLineAsync();
        }
    }
}