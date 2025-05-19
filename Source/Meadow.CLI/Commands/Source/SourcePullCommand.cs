using CliFx.Attributes;
using Meadow.Tools;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("source pull", Description = "Pulls each of the local Meadow source repositories")]
public class SourcePullCommand : BaseCommand<AppBuildCommand>
{
    private ISettingsManager _settingsManager;

    public SourcePullCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        var root = new MeadowRoot(_settingsManager);

        if (root.Pull() == false)
        {
            throw new CommandException("Failed to pull source repositories, ensure you've cloned first before pulling", CommandExitCode.GeneralError);
        }

        return default;
    }
}
