using CliFx.Attributes;
using Meadow.Hcom;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app run", Description = "Builds, trims and deploys a Meadow application to a target device")]
public class AppRunCommand : BaseDeviceCommand<AppRunCommand>
{
    private readonly IPackageManager _packageManager;
    private string? _lastFile;

    [CommandOption("no-prefix", 'n', Description = "When set, the message source prefix (e.g. 'stdout>') is suppressed during 'listen'", IsRequired = false)]
    public bool NoPrefix { get; init; }

    [CommandOption('c', Description = "The build configuration to compile", IsRequired = false)]
    public string? Configuration { get; set; }

    [CommandParameter(0, Name = "Path to folder containing the built application", IsRequired = false)]
    public string? Path { get; init; }

    public AppRunCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null)
        {
            return;
        }

        string path = Path ?? AppDomain.CurrentDomain.BaseDirectory;

        if (!Directory.Exists(path))
        {
            Logger?.LogError($"Target directory '{path}' not found");
            return;
        }

        var lastFile = string.Empty;

        // in order to deploy, the runtime must be disabled
        var wasRuntimeEnabled = await connection.IsRuntimeEnabled();
        if (wasRuntimeEnabled)
        {
            Logger?.LogInformation("Disabling runtime...");

            await connection.RuntimeDisable(CancellationToken);
        }

        if (!await BuildApplication(path, CancellationToken))
        {
            return;
        }

        if (!await TrimApplication(path, CancellationToken))
        {
            return;
        }

        // illink returns before all files are written - attempt a delay of 1s
        await Task.Delay(1000);

        if (!await DeployApplication(connection, path, CancellationToken))
        {
            return;
        }

        Logger?.LogInformation("Enabling the runtime...");
        await connection.RuntimeEnable(CancellationToken);

        Logger?.LogInformation("Listening for messages from Meadow...\n");
        connection.DeviceMessageReceived += OnDeviceMessageReceived;

        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }

        Logger?.LogInformation("Listen cancelled...");
    }

    private Task<bool> BuildApplication(string path, CancellationToken cancellationToken)
    {
        if (Configuration == null) { Configuration = "Debug"; }

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        // TODO: enable cancellation of this call
        return Task.FromResult(_packageManager.BuildApplication(path, Configuration));
    }

    private async Task<bool> TrimApplication(string path, CancellationToken cancellationToken)
    {
        // it's a directory - we need to determine the latest build (they might have a Debug and a Release config)
        var candidates = Package.PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        // if no configuration was provided, find the most recently built
        Logger?.LogInformation($"Trimming {file.FullName}");
        Logger?.LogInformation("This may take a few seconds...");

        var cts = new CancellationTokenSource();
        ConsoleSpinner.Spin(Console, cancellationToken: cts.Token);

        await _packageManager.TrimApplication(file, false, null, CancellationToken);
        cts.Cancel();

        return true;
    }

    private async Task<bool> DeployApplication(IMeadowConnection connection, string path, CancellationToken _)
    {
        connection.FileWriteProgress += OnFileWriteProgress;

        var candidates = Package.PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        Logger?.LogInformation($"Deploying app from {file.DirectoryName}...");

        await AppManager.DeployApplication(_packageManager, connection, file.DirectoryName!, true, false, Logger, CancellationToken);

        connection.FileWriteProgress -= OnFileWriteProgress;

        return true;
    }

    private void OnFileWriteProgress(object? sender, (string fileName, long completed, long total) e)
    {
        var p = e.completed / (double)e.total * 100d;

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