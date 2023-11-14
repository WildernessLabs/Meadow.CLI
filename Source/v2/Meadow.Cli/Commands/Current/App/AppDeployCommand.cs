using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app deploy", Description = "Deploys a built Meadow application to a target device")]
public class AppDeployCommand : BaseAppCommand<AppDeployCommand>
{
    private string lastFile = string.Empty;

    [CommandParameter(0, Name = "Path to folder containing the built application", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppDeployCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(packageManager, connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            string path = Path == null
                ? Environment.CurrentDirectory
                : Path;

            // is the path a file?
            FileInfo file;

            lastFile = string.Empty;

            // in order to deploy, the runtime must be disabled
            var wasRuntimeEnabled = await Connection.IsRuntimeEnabled();

            if (wasRuntimeEnabled)
            {
                Logger?.LogInformation("Disabling runtime...");

                await Connection.RuntimeDisable(CancellationToken);
            }

            if (!File.Exists(path))
            {
                // is it a valid directory?
                if (!Directory.Exists(path))
                {
                    Logger?.LogError($"Invalid application path '{path}'");
                    return;
                }

                // does the directory have an App.dll in it?
                file = new FileInfo(System.IO.Path.Combine(path, "App.dll"));
                if (!file.Exists)
                {
                    // it's a directory - we need to determine the latest build (they might have a Debug and a Release config)
                    var candidates = PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

                    if (candidates.Length == 0)
                    {
                        Logger?.LogError($"Cannot find a compiled application at '{path}'");
                        return;
                    }

                    file = candidates.OrderByDescending(c => c.LastWriteTime).First();
                }
            }
            else
            {
                // TODO: only deploy if it's App.dll
                file = new FileInfo(path);
            }

            var targetDirectory = file.DirectoryName;

            if (Logger != null && !string.IsNullOrEmpty(targetDirectory))
            {
                var trimApplicationCommand = new AppTrimCommand(_packageManager, ConnectionManager, LoggerFactory!)
                {
                    Path = path,
                };
                await trimApplicationCommand.ExecuteAsync(Console!);

                var localFiles = await AppManager.GenerateDeployList(_packageManager, targetDirectory, targetDirectory.Contains("Debug"), false, Logger, CancellationToken)
                    .WithSpinner(Console!);
                Console?.Output.WriteAsync("\n");

                Connection.FileWriteProgress += Connection_FileWriteProgress;

                await AppManager.DeployApplication(Connection, localFiles, Logger, CancellationToken);
                Console?.Output.WriteAsync("\n");

                Connection.FileWriteProgress -= Connection_FileWriteProgress;
            }

            if (wasRuntimeEnabled)
            {
                // restore runtime state
                Logger?.LogInformation("Enabling runtime...");

                await Connection.RuntimeEnable(CancellationToken);
            }
        }
    }

    private void Connection_FileWriteProgress(object? sender, (string fileName, long completed, long total) e)
    {
        var p = (e.completed / (double)e.total) * 100d;

        if (e.fileName != lastFile)
        {
            Console?.Output.WriteAsync("\n");
            lastFile = e.fileName;
        }

        // Console instead of Logger due to line breaking for progress bar
        Console?.Output.WriteAsync($"Writing {e.fileName}: {p:0}%         \r");
    }
}