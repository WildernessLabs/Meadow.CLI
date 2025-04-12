using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app build", Description = "Compile a Meadow application")]
public class AppBuildCommand : BaseCommand<AppBuildCommand>
{
    private readonly IBuildManager _buildManager;

    [CommandOption('c', Description = Strings.BuildConfiguration, IsRequired = false)]
    public string? Configuration { get; private set; }

    [CommandParameter(0, Description = Strings.PathToMeadowProject, IsRequired = false)]
    public string? Path { get; init; }

    public AppBuildCommand(IBuildManager buildManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _buildManager = buildManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        var path = AppTools.ValidateAndSanitizeAppPath(Path);

        Configuration ??= "Release";

        Logger?.LogInformation($"Building {Configuration} configuration of {path}...");

        var success = _buildManager.BuildApplication(path, Configuration);

        if (!success)
        {
            foreach (var line in _buildManager.BuildErrorText)
            {
                Logger?.LogInformation(line);
            }
            throw new CommandException("Build failed", CommandExitCode.GeneralError);
        }
        else
        {
            Logger?.LogInformation($"Build successful");
        }

        return default;
    }
}