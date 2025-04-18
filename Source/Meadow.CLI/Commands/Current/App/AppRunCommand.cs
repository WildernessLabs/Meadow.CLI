﻿using CliFx.Attributes;
using Meadow.Hcom;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app run", Description = "Build, trim and deploy a Meadow application to a target device")]
public class AppRunCommand : BaseDeviceCommand<AppRunCommand>
{
    private readonly IBuildManager _buildManager;

    [CommandOption("prefix", 'p', Description = "When set, the message source prefix (e.g. 'stdout>') is shown during 'listen'", IsRequired = false)]
    public bool Prefix { get; init; } = false;

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathMeadowApplication, IsRequired = false)]
    public string? Path { get; init; }

    [CommandOption("nolink", Description = Strings.NoLinkAssemblies, IsRequired = false)]
    public string[]? NoLink { get; private set; }

    readonly FileManager _fileManager;

    public AppRunCommand(FileManager fileManager, IBuildManager buildManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _buildManager = buildManager;
        _fileManager = fileManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        await _fileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = _fileManager.Firmware["Meadow F7"];

        if (collection == null || !collection.Any())
        {
            throw new CommandException(Strings.NoFirmwarePackagesFound, CommandExitCode.GeneralError);
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

        // in order to deploy, the runtime must be disabled
        await AppTools.DisableRuntimeIfEnabled(connection, Logger, CancellationToken);

        Logger?.LogInformation($"Building {Configuration} configuration of {path} for Meadow v{deviceInfo.OsVersion}...");

        if (!_buildManager.BuildApplication(path, Configuration))
        {
            foreach (var line in _buildManager.BuildErrorText)
            {
                Logger?.LogInformation(line);
            }
            throw new CommandException(Strings.AppBuildFailed, CommandExitCode.GeneralError);
        }

        if (!await AppTools.TrimApplication(path, _buildManager, deviceInfo.OsVersion, Configuration, NoLink, Logger, Console, CancellationToken))
        {
            throw new CommandException(Strings.AppTrimFailed, CommandExitCode.GeneralError);
        }

        if (!await DeployApplication(connection, path, Configuration, CancellationToken))
        {
            throw new CommandException(Strings.AppDeployFailed, CommandExitCode.GeneralError);
        }

        Logger?.LogInformation($"{Strings.EnablingRuntime}...");
        await connection.RuntimeEnable(CancellationToken);

        Logger?.LogInformation("Listening for messages from Meadow...\n");
        connection.DeviceMessageReceived += OnDeviceMessageReceived;

        while (!CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }

        Logger?.LogInformation("Listen cancelled...");
    }

    private async Task<bool> DeployApplication(IMeadowConnection connection, string path, string configuration, CancellationToken cancellationToken)
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

        //get the file that matches the configuration
        var file = candidates.FirstOrDefault(c => c.DirectoryName.Contains(configuration, StringComparison.OrdinalIgnoreCase));

        if (file == null)
        {
            Logger?.LogError($"Cannot find a compiled application for configuration '{configuration}'");
            return false;
        }

        Logger?.LogInformation($"Deploying app from {file.DirectoryName}...");

        await AppManager.DeployApplication(_buildManager, connection, deviceInfo.OsVersion, file.DirectoryName!, true, false, Logger, cancellationToken);

        connection.FileWriteProgress -= OnFileWriteProgress;

        return true;
    }

    private void OnFileWriteProgress(object? sender, (string fileName, long completed, long total) e)
    {
        var p = e.completed / (double)e.total * 100d;

        if (!double.IsNaN(p))
        {   // Console instead of Logger due to line breaking for progress bar
            Console?.Output.Write($"Writing  '{e.fileName}': {p:0}%         \r");
        }
    }

    private void OnDeviceMessageReceived(object? sender, (string message, string? source) e)
    {
        if (Prefix)
        {
            Logger?.LogInformation($"{e.source}> {e.message.TrimEnd('\n', '\r')}");
        }
        else
        {
            Logger?.LogInformation($"{e.message.TrimEnd('\n', '\r')}");
        }
    }
}