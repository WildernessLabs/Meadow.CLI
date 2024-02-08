using CliFx.Attributes;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app build", Description = "Compile a Meadow application")]
public class AppBuildCommand : BaseCommand<AppBuildCommand>
{
    private readonly IPackageManager _packageManager;

    [CommandOption('c', Description = "The build configuration to compile", IsRequired = false)]
    public string? Configuration { get; set; }

    [CommandParameter(0, Name = "Path to project file", IsRequired = false)]
    public string? Path { get; init; }

    public AppBuildCommand(IPackageManager packageManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        string path = Path ?? AppDomain.CurrentDomain.BaseDirectory;

        // is the path a file?
        if (!File.Exists(path))
        {
            // is it a valid directory?
            if (!Directory.Exists(path))
            {
                Logger?.LogError($"Invalid application path '{path}'");
                return ValueTask.CompletedTask;
            }
        }

        Configuration ??= "Release";

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        // TODO: enable cancellation of this call
        var success = _packageManager.BuildApplication(path, Configuration);

        if (!success)
        {
            Logger?.LogError($"Build failed!");
        }
        else
        {
            Logger?.LogInformation($"Build successful");
        }
        return ValueTask.CompletedTask;
    }
}