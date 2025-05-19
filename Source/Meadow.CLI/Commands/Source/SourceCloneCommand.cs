using CliFx.Attributes;
using Meadow.Tools;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("source clone", Description = "Clones any missing local Meadow source repositories")]
public class SourceCloneCommand : BaseCommand<AppBuildCommand>
{
    private ISettingsManager _settingsManager;

    public SourceCloneCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        var root = new MeadowRoot(_settingsManager);

        if (root.Clone() == false)
        {
            throw new CommandException("Failed to clone source repositories", CommandExitCode.GeneralError);
        }

        return default;
    }
}