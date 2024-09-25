using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("config route", Description = "Sets the communication route for HCOM")]
public class ConfigRouteCommand : BaseSettingsCommand<ConfigCommand>
{
    [CommandParameter(0, Name = "Route", IsRequired = true)]
    public string Route { get; init; }

    public ConfigRouteCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    { }


    protected override ValueTask ExecuteCommand()
    {
        Logger?.LogInformation($"{Environment.NewLine}Setting route={Route}");
        SettingsManager.SaveSetting("route", Route);

        return ValueTask.CompletedTask;
    }
}
