using CliFx.Attributes;
using Meadow.Tools;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("source fetch", Description = "Fetches each of the local Meadow source repositories")]
public class SourceFetchCommand : BaseCommand<AppBuildCommand>
{
    private ISettingsManager _settingsManager;

    public SourceFetchCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        var root = new MeadowRoot(_settingsManager);

        root.Fetch();

        return default;
    }
}
