using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app run", Description = "Builds, trims and deploys a Meadow application to a target device")]
public class AppRunCommand : BaseAppCommand<AppRunCommand>
{
    [CommandOption("no-prefix", 'n', IsRequired = false, Description = "When set, the message source prefix (e.g. 'stdout>') is suppressed during 'listen'")]
    public bool NoPrefix { get; set; }

    [CommandOption('c', Description = "The build configuration to compile", IsRequired = false)]
    public string? Configuration { get; set; }

    [CommandParameter(0, Name = "Path to folder containing the built application", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppRunCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(packageManager, connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        string path = Path == null
            ? Environment.CurrentDirectory
            : Path;

        if (!Directory.Exists(path))
        {
            Logger?.LogError($"Target directory '{path}' not found.");
            return;
        }

        var lastFile = string.Empty;

        var buildmApplicationCommand = new AppBuildCommand(_packageManager, LoggerFactory!)
        {
            Path = path
        };
        await buildmApplicationCommand.ExecuteAsync(Console!);


        if (Connection != null)
        {
            // illink returns before all files are actually written.  That's not fun, but we must just wait a little while.
            // disabling the runtime provides us that time

            // in order to deploy, the runtime must be disabled
            var wasRuntimeEnabled = await Connection.IsRuntimeEnabled();

            Logger?.LogInformation("Disabling runtime...");

            await Connection.RuntimeDisable(CancellationToken);

            if (Connection is SerialConnection s)
            {
                s.CommandTimeoutSeconds = 60;
            }

            var deployApplication = new AppDeployCommand(_packageManager, ConnectionManager, LoggerFactory!)
            {
                Path = path
            };
            await deployApplication.ExecuteAsync(Console!);

            Logger?.LogInformation("Enabling the runtime...");
            await Connection.RuntimeEnable(CancellationToken);

            Logger?.LogInformation("Listening for messages from Meadow...\n");
            Connection.DeviceMessageReceived += OnDeviceMessageReceived;

            while (!CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }

            Logger?.LogInformation("Listen cancelled...");
        }
    }

    private void OnDeviceMessageReceived(object? sender, (string message, string? source) e)
    {
        if (NoPrefix)
        {
            Logger?.LogInformation($"{e.message.TrimEnd('\n', '\r')}");
        }
        else
        {
            Logger?.LogInformation($"{e.source}> {e.message.TrimEnd('\n', '\r')}");
        }
    }
}
