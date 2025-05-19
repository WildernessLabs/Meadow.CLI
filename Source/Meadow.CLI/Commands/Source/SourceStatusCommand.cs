using CliFx.Attributes;
using Meadow.Tools;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("source status", Description = "Compares the local Meadow source repositories with the remotes")]
public class SourceStatusCommand : BaseCommand<AppBuildCommand>
{
    private ISettingsManager _settingsManager;

    public SourceStatusCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        var root = new MeadowRoot(_settingsManager);

        root.Status();

        return default;
    }
}
