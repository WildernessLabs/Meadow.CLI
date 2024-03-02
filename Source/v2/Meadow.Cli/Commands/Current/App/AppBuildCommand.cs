using CliFx.Attributes;
using Meadow.Package;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app build", Description = "Compile a Meadow application")]
public class AppBuildCommand : BaseCommand<AppBuildCommand>
{
    private readonly IPackageManager _packageManager;

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathToMeadowProject, IsRequired = false)]
    public string? Path { get; init; }

    public AppBuildCommand(IPackageManager packageManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var path = AppTools.ValidateAndSanitizeAppPath(Path);

        Configuration ??= "Release";

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        var success = _packageManager.BuildApplication(path, Configuration);

        if (!success)
        {
            throw new CommandException("Build failed", CommandExitCode.GeneralError);
        }
        else
        {
            Logger?.LogInformation($"Build successful");
        }
    }
}