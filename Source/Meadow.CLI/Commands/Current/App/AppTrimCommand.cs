using CliFx.Attributes;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app trim", Description = "Trim a pre-compiled Meadow application")]
public class AppTrimCommand : BaseDeviceCommand<AppTrimCommand>
{
    private readonly IBuildManager _buildManager;

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathToMeadowProject, IsRequired = false)]
    public string? Path { get; init; }

    [CommandOption("nolink", Description = Strings.NoLinkAssemblies, IsRequired = false)]
    public string[]? NoLink { get; private set; }

    readonly FileManager _fileManager;

    public AppTrimCommand(FileManager fileManager, IBuildManager buildManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
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

        if (collection == null || collection.Count() == 0)
        {
            throw new CommandException(Strings.NoFirmwarePackagesFound, CommandExitCode.GeneralError);
        }

        var path = AppTools.ValidateAndSanitizeAppPath(Path);

        if (!File.Exists(path))
        {
            // is it a valid directory?
            if (!Directory.Exists(path))
            {
                throw new CommandException($"{Strings.InvalidApplicationPath} '{path}'", CommandExitCode.FileNotFound);
            }
        }

        var connection = await GetCurrentConnection();

        await AppTools.DisableRuntimeIfEnabled(connection, Logger, CancellationToken);

        var deviceInfo = await connection.GetDeviceInfo();

        if (deviceInfo == null || deviceInfo.OsVersion == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo, CommandExitCode.GeneralError);
        }

        var package = collection.GetClosestLocalPackage(deviceInfo.OsVersion);

        Logger.LogInformation($"Preparing to trim using v{package?.Version ?? " unknown"} assemblies...");
        await AppTools.TrimApplication(path, _buildManager, deviceInfo.OsVersion, Configuration, NoLink, Logger, Console, CancellationToken);
        Logger.LogInformation("Application trimmed successfully");
    }
}