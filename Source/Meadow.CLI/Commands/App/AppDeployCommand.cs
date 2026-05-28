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
        var path = AppTools.ValidateAndSanitizeAppPath(Path);
        var configuration = Configuration ?? "Release";

        var connection = await GetCurrentConnection();

        await AppTools.DisableRuntimeIfEnabled(connection, Logger, CancellationToken);

        var deviceInfo = await connection.GetDeviceInfo();

        if (deviceInfo == null || deviceInfo.OsVersion == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo, CommandExitCode.GeneralError);
        }

        var isV3 = MeadowVersion.IsV3OrLater(deviceInfo.OsVersion);
        var deployDirectory = ResolveDeployDirectory(path, configuration, isV3);

        await DeployApplication(connection, deviceInfo.OsVersion, deployDirectory, configuration, CancellationToken);
    }

    // Locates the already-compiled output to deploy. 'app deploy' never builds - that's 'app run' -
    // so this resolves an existing output for the requested configuration and fails clearly if none exists.
    private string ResolveDeployDirectory(string path, string configuration, bool isV3)
    {
        // a direct path to App.dll
        if (File.Exists(path))
        {
            if (string.Compare(System.IO.Path.GetFileName(path), AppFileName, true) != 0)
            {
                throw new CommandException($"The file '{path}' is not a compiled Meadow application", CommandExitCode.FileNotFound);
            }
            return System.IO.Path.GetDirectoryName(path)!;
        }

        if (!Directory.Exists(path))
        {
            throw new CommandException($"{Strings.InvalidApplicationPath} '{path}'", CommandExitCode.FileNotFound);
        }

        // a directory that directly holds App.dll (e.g. a publish folder passed explicitly)
        if (File.Exists(System.IO.Path.Combine(path, AppFileName)))
        {
            return path;
        }

        // otherwise locate the build output for the requested configuration
        var candidates = PackageManager.GetAvailableBuiltConfigurations(path, AppFileName)
            .Where(c => HasPathSegment(c.DirectoryName, configuration))
            .ToList();

        if (candidates.Count == 0)
        {
            throw new CommandException($"Cannot find a compiled '{configuration}' application at '{path}'", CommandExitCode.FileNotFound);
        }

        if (isV3)
        {
            // 3.x must deploy the publish output - it has the Meadow BCL injected and trimming applied.
            // Deploying a plain build output would boot-fail silently.
            var publish = candidates.FirstOrDefault(c => HasPathSegment(c.DirectoryName, "publish"));

            if (publish == null)
            {
                throw new CommandException(
                    $"No publish output found for the '{configuration}' configuration at '{path}'. Run 'meadow app run' to build and deploy.",
                    CommandExitCode.FileNotFound);
            }

            return publish.DirectoryName!;
        }

        // 2.x deploys the build output (newest if multiple target frameworks exist)
        return candidates.OrderByDescending(c => c.LastWriteTime).First().DirectoryName!;
    }

    private static bool HasPathSegment(string? directory, string segment)
    {
        return directory != null
            && directory.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                .Any(s => s.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }

    private async Task DeployApplication(IMeadowConnection connection, string osVersion, string deployDirectory, string configuration, CancellationToken cancellationToken)
    {
        // only deploy PDBs for Debug builds - they're dead weight on a Release deployment
        var includePdbs = configuration.Equals("Debug", StringComparison.OrdinalIgnoreCase);

        connection.FileWriteProgress += OnFileWriteProgress;
        try
        {
            Logger?.LogInformation($"Deploying app from {deployDirectory}...");

            if (MeadowVersion.IsV3OrLater(osVersion))
            {
                await AppManagerV3.DeployApplication(connection, deployDirectory, includePdbs, false, Logger, cancellationToken);
            }
            else
            {
                await AppManager.DeployApplication(_buildManager, connection, osVersion, deployDirectory, includePdbs, false, Logger, cancellationToken);
            }
        }
        finally
        {
            connection.FileWriteProgress -= OnFileWriteProgress;
        }

        Logger?.LogInformation($"{Strings.AppDeployedSuccessfully}");
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