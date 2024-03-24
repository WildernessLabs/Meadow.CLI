using CliFx.Attributes;
using Meadow.Hcom;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app run", Description = "Build, trim and deploy a Meadow application to a target device")]
public class AppRunCommand : BaseDeviceCommand<AppRunCommand>
{
    private readonly IPackageManager _packageManager;
    private string? _lastFile;

    [CommandOption("no-prefix", 'n', Description = "When set, the message source prefix (e.g. 'stdout>') is suppressed during 'listen'", IsRequired = false)]
    public bool NoPrefix { get; init; }

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathMeadowApplication, IsRequired = false)]
    public string? Path { get; init; }

    [CommandOption("nolink", Description = Strings.NoLinkAssemblies, IsRequired = false)]
    public string[]? NoLink { get; private set; }

    public AppRunCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var path = AppTools.ValidateAndSanitizeAppPath(Path);

        Configuration ??= "Release";

        var connection = await GetCurrentConnection();

        var lastFile = string.Empty;

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        if (!_packageManager.BuildApplication(path, Configuration))
        {
            throw new CommandException("Application build failed", CommandExitCode.GeneralError);
        }

        if (!await AppTools.TrimApplication(path, _packageManager, Configuration, NoLink, Logger, Console, CancellationToken))
        {
            throw new CommandException("Application trimming failed", CommandExitCode.GeneralError);
        }

        if (!await DeployApplication(connection, path, CancellationToken))
        {
            throw new CommandException("Application deploy failed", CommandExitCode.GeneralError);
        }

        Logger?.LogInformation("Listening for messages from Meadow...\n");
        connection.DeviceMessageReceived += OnDeviceMessageReceived;

        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }

        Logger?.LogInformation("Listen cancelled...");
    }

    private async Task<bool> DeployApplication(IMeadowConnection connection, string path, CancellationToken cancellationToken)
    {
        connection.FileWriteProgress += OnFileWriteProgress;

        var candidates = PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        Logger?.LogInformation($"Deploying app from {file.DirectoryName}...");

        await AppManager.DeployApplication(_packageManager, connection, file.DirectoryName!, true, false, Logger, cancellationToken);

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