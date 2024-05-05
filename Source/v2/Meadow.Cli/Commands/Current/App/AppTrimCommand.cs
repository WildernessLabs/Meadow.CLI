using CliFx.Attributes;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app trim", Description = "Trim a pre-compiled Meadow application")]
public class AppTrimCommand : BaseDeviceCommand<AppTrimCommand>
{
    private readonly IPackageManager _packageManager;

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathToMeadowProject, IsRequired = false)]
    public string? Path { get; init; }

    [CommandOption("nolink", Description = Strings.NoLinkAssemblies, IsRequired = false)]
    public string[]? NoLink { get; private set; }

    readonly FileManager _fileManager;

    public AppTrimCommand(FileManager fileManager, IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
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

        Logger.LogInformation($"Preparing to trim using v{deviceInfo.OsVersion} assemblies...");
        await AppTools.TrimApplication(path, _packageManager, deviceInfo.OsVersion, Configuration, NoLink, Logger, Console, CancellationToken);
        Logger.LogInformation("Application trimmed successfully");
    }
}