using CliFx.Attributes;
using Meadow.Hcom;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app deploy", Description = "Deploys a previously compiled Meadow application to a target device")]
public class AppDeployCommand : BaseDeviceCommand<AppDeployCommand>
{
    private readonly IBuildManager _buildManager;

    private readonly string AppFileName = "App.dll";

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathMeadowApplication, IsRequired = false)]
    public string? Path { get; init; }

    public AppDeployCommand(IBuildManager buildManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        _buildManager = buildManager;
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

        if (!await DeployApplication(connection, deviceInfo.OsVersion, path, file, CancellationToken))
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

    private async Task<bool> DeployApplication(IMeadowConnection connection, string osVersion, string projectPath, FileInfo appFile, CancellationToken cancellationToken)
    {
        connection.FileWriteProgress += OnFileWriteProgress;

        // only deploy PDBs for Debug builds - they're dead weight on a Release deployment
        var includePdbs = (Configuration ?? "Release").Equals("Debug", StringComparison.OrdinalIgnoreCase);

        if (MeadowVersion.IsV3OrLater(osVersion))
        {
            var appDir = appFile.DirectoryName!;
            var publishDir = System.IO.Path.GetFileName(appDir) == "publish"
                ? appDir
                : System.IO.Path.Combine(appDir, "publish");

            // Always publish for V3 — the publish step injects Meadow's custom BCL
            // and configures trimming. Skipping it (e.g. after a manual dotnet publish)
            // would deploy without BCL assemblies, causing silent boot failures.
            Logger?.LogInformation("Publishing with Meadow BCL injection and trimming...");

            // projectPath may be a directory, a path to App.dll, or a path to a csproj.
            // Walk up from the chosen App.dll until a csproj is found so we can publish that project.
            var publishPath = FindProjectFile(projectPath, appFile);
            if (publishPath == null)
            {
                Logger?.LogError($"Cannot locate a .csproj file from '{projectPath}'. Specify the path to your project directory or .csproj.");
                return false;
            }

            if (!_buildManager.PublishApplication(publishPath, osVersion, Configuration ?? "Release", clean: false, publishDir: publishDir + System.IO.Path.DirectorySeparatorChar))
            {
                Logger?.LogError("Publish failed. Build output:");
                foreach (var line in _buildManager.BuildErrorText)
                {
                    Logger?.LogError(line);
                }
                return false;
            }

            if (!Directory.Exists(publishDir))
            {
                Logger?.LogError($"Cannot find publish output at '{publishDir}'. Ensure the project published successfully.");
                return false;
            }

            Logger?.LogInformation($"Deploying app from {publishDir}...");
            await AppManagerV3.DeployApplication(connection, publishDir, includePdbs, false, Logger, cancellationToken);
        }
        else
        {
            Logger?.LogInformation($"Deploying app from {appFile.DirectoryName}...");
            await AppManager.DeployApplication(_buildManager, connection, osVersion, appFile.DirectoryName!, includePdbs, false, Logger, cancellationToken);
        }

        connection.FileWriteProgress -= OnFileWriteProgress;

        Logger?.LogInformation($"{Strings.AppDeployedSuccessfully}");

        return true;
    }

    private static string? FindProjectFile(string projectPath, FileInfo appFile)
    {
        // If the caller passed a csproj directly, use it.
        if (File.Exists(projectPath) && projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return projectPath;
        }

        // Otherwise search the project path (if a directory) and walk up from the App.dll until a csproj is found.
        var startDir = Directory.Exists(projectPath) ? projectPath : appFile.DirectoryName;
        var dir = startDir;
        while (dir != null)
        {
            var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
            if (csproj != null) return csproj;
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return null;
    }

    private void OnFileWriteProgress(object? sender, (string fileName, long completed, long total) e)
    {
        var p = e.completed / (double)e.total * 100d;

        if (!double.IsNaN(p))
        {
            // Console instead of Logger due to line breaking for progress bar
            Console?.Output.Write($"Writing  '{e.fileName}': {p:0}%         \r");
        }
    }
}