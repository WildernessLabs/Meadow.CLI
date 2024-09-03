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

        root.Clone();

        return default;
    }
}

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

        root.Pull();

        return default;
    }
}
