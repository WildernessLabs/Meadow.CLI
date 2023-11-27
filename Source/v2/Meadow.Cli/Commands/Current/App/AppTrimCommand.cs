using System.Threading;
using CliFx.Attributes;
using Meadow.CLI;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app trim", Description = "Runs an already-compiled Meadow application through reference trimming")]
public class AppTrimCommand : BaseAppCommand<AppTrimCommand>
{
    [CommandOption('c', Description = "The build configuration to trim", IsRequired = false)]
    public string? Configuration { get; set; }

    [CommandParameter(0, Name = "Path to project file", IsRequired = false)]
    public string? Path { get; set; } = default!;

    public AppTrimCommand(IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(packageManager, connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

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

        // Find RuntimeVersion
        if (Connection != null)
        {
            var info = await Connection.GetDeviceInfo(CancellationToken);

            _packageManager.RuntimeVersion = info?.RuntimeVersion;

            if (!string.IsNullOrWhiteSpace(_packageManager.MeadowAssembliesPath) && Directory.Exists(_packageManager.MeadowAssembliesPath))
            {
                Logger?.LogInformation($"Using runtime files from {_packageManager.MeadowAssembliesPath}");

                // Avoid double reporting.
                DetachMessageHandlers(Connection);
            }
            else
            {
                Logger?.LogError($"Meadow Assemblies Path: '{_packageManager.MeadowAssembliesPath}' does NOT exist for Runtime: '{_packageManager.RuntimeVersion}'.");
                return;
            }

        }

        // TODO: support `nolink` command line args
        await _packageManager.TrimApplication(file, false, null, Logger, CancellationToken)
            .WithSpinner(Console!);
    }
}