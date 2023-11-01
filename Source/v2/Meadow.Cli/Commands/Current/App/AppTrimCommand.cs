using CliFx.Attributes;
using Meadow.CLI;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app trim", Description = "Runs an already-compiled Meadow application through reference trimming")]
public class AppTrimCommand : BaseCommand<AppTrimCommand>
{
    private IPackageManager _packageManager;

    [CommandOption('c', Description = "The build configuration to trim", IsRequired = false)]
    public string? Configuration { get; set; }

    [CommandParameter(0, Name = "Path to project file", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppTrimCommand(IPackageManager packageManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        string path = Path == null
            ? Environment.CurrentDirectory
            : Path;

        // is the path a file?
        FileInfo file;

        if (!File.Exists(path))
        {
            // is it a valid directory?
            if (!Directory.Exists(path))
            {
                Logger?.LogError($"Invalid application path '{path}'");
                return;
            }

            // it's a directory - we need to determine the latest build (they might have a Debug and a Release config)
            var candidates = PackageManager.GetAvailableBuiltConfigurations(path, "App.dll");

            if (candidates.Length == 0)
            {
                Logger?.LogError($"Cannot find a compiled application at '{path}'");
                return;
            }

            file = candidates.OrderByDescending(c => c.LastWriteTime).First();
        }
        else
        {
            file = new FileInfo(path);
        }

        // if no configuration was provided, find the most recently built
        Logger?.LogInformation($"Trimming {file.FullName} (this may take a few seconds)...");

        // TODO: support `nolink` command line args
        await _packageManager.TrimApplication(file, false, null, Logger, CancellationToken);
    }
}
