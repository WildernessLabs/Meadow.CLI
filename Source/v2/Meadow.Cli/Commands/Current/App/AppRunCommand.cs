using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app run", Description = "Builds, trims and deploys a Meadow application to a target device")]
public class AppRunCommand : BaseDeviceCommand<AppRunCommand>
{
    private IPackageManager _packageManager;
    private string _lastFile = string.Empty;

    [CommandOption("no-prefix", 'n', IsRequired = false, Description = "When set, the message source prefix (e.g. 'stdout>') is suppressed during 'listen'")]
    public bool NoPrefix { get; set; }

    [CommandOption('c', Description = "The build configuration to compile", IsRequired = false)]
    public string? Configuration { get; set; }

    [CommandParameter(0, Name = "Path to folder containing the built application", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppRunCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        string path = Path == null
            ? Environment.CurrentDirectory
            : Path;

        if (!Directory.Exists(path))
        {
            Logger?.LogError($"Target directory '{path}' not found.");
            return;
        }

        var lastFile = string.Empty;

        if (!await BuildApplication(path, CancellationToken))
        {
            Logger?.LogError("Build failed.");
            return;
        }

        if (!await TrimApplication(path, CancellationToken))
        {
            Logger?.LogError("Trimming failed.");
            return;
        }

        await base.ExecuteCommand();


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

            if (!await DeployApplication(Connection, path, CancellationToken))
            {
                return;
            }

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

    private Task<bool> BuildApplication(string path, CancellationToken cancellationToken)
    {
        if (Configuration == null) Configuration = "Debug";

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        // TODO: enable cancellation of this call
        return Task.FromResult(_packageManager.BuildApplication(path, Configuration, logger: Logger));
    }

    private async Task<bool> TrimApplication(string path, CancellationToken cancellationToken)
    {
        // it's a directory - we need to determine the latest build (they might have a Debug and a Release config)
        var candidates = Cli.PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        // if no configuration was provided, find the most recently built
        Logger?.LogInformation($"Trimming {file.FullName} (this may take a few seconds)...");

        await _packageManager.TrimApplication(file, false, null, Logger, cancellationToken);

        return true;
    }

    private async Task<bool> DeployApplication(IMeadowConnection connection, string path, CancellationToken cancellationToken)
    {
        connection.FileWriteProgress += OnFileWriteProgress;

        var candidates = Cli.PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();
        var directoryName = file.DirectoryName;

        Logger?.LogInformation($"Deploying app from {directoryName}...");

        if (!string.IsNullOrEmpty(directoryName) && Logger != null)
        {
            await AppManager.DeployApplication(_packageManager, connection, directoryName, true, false, Logger, CancellationToken);
        }
        else
        {
            Logger?.LogError($"Invalid DirectoryName");
        }

        connection.FileWriteProgress -= OnFileWriteProgress;

        return true;
    }

    private void OnFileWriteProgress(object? sender, (string fileName, long completed, long total) e)
    {
        var p = (e.completed / (double)e.total) * 100d;

        if (e.fileName != _lastFile)
        {
            Console?.Output.Write("\n");
            _lastFile = e.fileName;
        }

        // Console instead of Logger due to line breaking for progress bar
        Console?.Output.Write($"Writing {e.fileName}: {p:0}%         \r");
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
