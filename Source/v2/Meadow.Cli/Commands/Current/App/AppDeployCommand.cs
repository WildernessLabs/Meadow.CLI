using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app deploy", Description = "Deploys a built Meadow application to a target device")]
public class AppDeployCommand : BaseDeviceCommand<AppDeployCommand>
{
    [CommandParameter(0, Name = "Path to folder containing the built application", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppDeployCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
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

        // do we have the full app path, or just the project root?

        // TODO: determine the latest build

        await AppManager.DeployApplication(connection, "", true, false, Logger, cancellationToken);

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
