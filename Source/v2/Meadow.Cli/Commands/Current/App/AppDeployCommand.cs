using CliFx.Attributes;
using Meadow.Cli;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app deploy", Description = "Deploys a built Meadow application to a target device")]
public class AppDeployCommand : BaseCommand<AppDeployCommand>
{
    private IPackageManager _packageManager;

    [CommandParameter(0, Name = "Path to folder containing the built application", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppDeployCommand(IPackageManager packageManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand(CancellationToken cancellationToken)
    {
        string path = Path == null
            ? AppDomain.CurrentDomain.BaseDirectory
            : Path;

        // is the path a file?
        if (!File.Exists(path))
        {
            // is it a valid directory?
            if (!Directory.Exists(path))
            {
                Logger.LogError($"Invalid application path '{path}'");
                return;
            }
        }
        else
        {
            // TODO: only deploy if it's App.dll
        }

        // TODO: send files

        var success = false;

        if (!success)
        {
            Logger.LogError($"Build failed!");
        }
        else
        {
            Logger.LogError($"Build success.");
        }
    }
}
