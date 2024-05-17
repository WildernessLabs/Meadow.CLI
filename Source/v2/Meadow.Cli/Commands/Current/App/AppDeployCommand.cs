using CliFx.Attributes;
using Meadow.Hcom;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app deploy", Description = "Deploy a compiled Meadow application to a target device")]
public class AppDeployCommand : BaseDeviceCommand<AppDeployCommand>
{
    private readonly IPackageManager _packageManager;

    private readonly string AppFileName = "App.dll";

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathMeadowApplication, IsRequired = false)]
    public string? Path { get; init; }

    public AppDeployCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        string path = AppTools.ValidateAndSanitizeAppPath(Path);

        var file = GetMeadowAppFile(path);

        var connection = await GetCurrentConnection();

        await AppTools.DisableRuntimeIfEnabled(connection, Logger, CancellationToken);

        var deviceInfo = await connection.GetDeviceInfo();

        if (deviceInfo == null || deviceInfo.OsVersion == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo, CommandExitCode.GeneralError);
        }

        if (!await DeployApplication(connection, deviceInfo.OsVersion, file.FullName, CancellationToken))
        {
            throw new CommandException(Strings.AppDeployFailed, CommandExitCode.GeneralError);
        }
    }

    private FileInfo GetMeadowAppFile(string path)
    {
        // is the path a file?
        FileInfo file;

        if (!File.Exists(path))
        {
            // is it a valid directory?
            if (!Directory.Exists(path))
            {
                throw new CommandException($"{Strings.InvalidApplicationPath} '{path}'", CommandExitCode.FileNotFound);
            }

            // does the directory have an App.dll in it?
            file = new FileInfo(System.IO.Path.Combine(path, AppFileName));
            if (!file.Exists)
            {
                // it's a directory - we need to determine the latest build (they might have a Debug and a Release config)
                var candidates = PackageManager.GetAvailableBuiltConfigurations(path, AppFileName);

                if (candidates.Length == 0)
                {
                    throw new CommandException($"Cannot find a compiled application at '{path}'", CommandExitCode.FileNotFound);
                }

                file = candidates.OrderByDescending(c => c.LastWriteTime).First();
            }
        }
        else
        {
            if (System.IO.Path.GetFileName(path) != AppFileName)
            {
                throw new CommandException($"The file '{path}' is not a compiled Meadow application", CommandExitCode.FileNotFound);
            }

            file = new FileInfo(path);
        }
        return file;
    }

    private async Task<bool> DeployApplication(IMeadowConnection connection, string osVersion, string path, CancellationToken GetAvailableBuiltConfigurations)
    {
        connection.FileWriteProgress += OnFileWriteProgress;

        var candidates = PackageManager.GetAvailableBuiltConfigurations(path, AppFileName);

        if (candidates.Length == 0)
        {
            Logger?.LogError($"Cannot find a compiled application at '{path}'");
            return false;
        }

        var file = candidates.OrderByDescending(c => c.LastWriteTime).First();

        Logger?.LogInformation($"Deploying app from {file.DirectoryName}...");

        await AppManager.DeployApplication(_packageManager, connection, osVersion, file.DirectoryName!, true, false, Logger, GetAvailableBuiltConfigurations);

        connection.FileWriteProgress -= OnFileWriteProgress;

        Logger?.LogInformation($"{Strings.AppDeployedSuccessfully}");

        return true;
    }

    private void OnFileWriteProgress(object? sender, (string fileName, long completed, long total) e)
    {
        var p = e.completed / (double)e.total * 100d;

        if (!double.IsNaN(p))
        {
            // Console instead of Logger due to line breaking for progress bar
            Console?.Output.Write($"Writing {e.fileName}: {p:0}%         \r");
        }
    }
}