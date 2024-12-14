using CliFx.Attributes;
using Meadow.Tools;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("source checkout", Description = "Pulls a single-named branch of all Meadow source repositories")]
public class SourceCheckoutCommand : BaseCommand<AppBuildCommand>
{
    private ISettingsManager _settingsManager;

    [CommandParameter(0, Description = Strings.PathToMeadowProject, IsRequired = true)]
    public string Branch { get; init; }

    public SourceCheckoutCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _settingsManager = settingsManager;
    }

    protected override ValueTask ExecuteCommand()
    {
        var root = new MeadowRoot(_settingsManager);

        root.Checkout(Branch);

        return default;
    }
}