using CliFx.Attributes;
using Meadow.Hcom;
using Meadow.Package;
using Meadow.Software;
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

    readonly FileManager _fileManager;

    public AppRunCommand(FileManager fileManager, IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _packageManager = packageManager;
        _fileManager = fileManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        await _fileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = _fileManager.Firmware["Meadow F7"];

        if (collection == null || collection.Count() == 0)
        {
            throw new CommandException(Strings.NoFirmwarePackagesFound, CommandExitCode.GeneralError);
        }

        if (collection.DefaultPackage == null)
        {
            throw new CommandException(Strings.NoDefaultFirmwarePackageSet, CommandExitCode.GeneralError);
        }

        var path = AppTools.ValidateAndSanitizeAppPath(Path);

        Configuration ??= "Release";

        var connection = await GetCurrentConnection();

        var deviceInfo = await connection.GetDeviceInfo();

        if (deviceInfo == null || deviceInfo.OsVersion == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo, CommandExitCode.GeneralError);
        }

        var lastFile = string.Empty;

        Logger?.LogInformation($"Building {Configuration} configuration of {path} for Meadow v{deviceInfo.OsVersion}...");

        if (!_packageManager.BuildApplication(path, Configuration))
        {
            throw new CommandException(Strings.AppBuildFailed, CommandExitCode.GeneralError);
        }

        if (!await AppTools.TrimApplication(path, _packageManager, deviceInfo.OsVersion, Configuration, NoLink, Logger, Console, CancellationToken))
        {
            throw new CommandException(Strings.AppTrimFailed, CommandExitCode.GeneralError);
        }

        if (!await DeployApplication(connection, path, CancellationToken))
        {
            throw new CommandException(Strings.AppDeployFailed, CommandExitCode.GeneralError);
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

        var deviceInfo = await connection.GetDeviceInfo();

        if (deviceInfo == null || deviceInfo.OsVersion == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo, CommandExitCode.GeneralError);
        }

        var candidates = PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        Logger?.LogInformation($"Deploying app from {file.DirectoryName}...");

        await AppManager.DeployApplication(_packageManager, connection, deviceInfo.OsVersion, file.DirectoryName!, true, false, Logger, cancellationToken);

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