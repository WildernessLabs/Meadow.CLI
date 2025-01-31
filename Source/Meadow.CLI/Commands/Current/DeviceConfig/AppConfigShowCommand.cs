using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("app config show", Description = "Shows the connected device's app.config.yaml settings")]
public class AppConfigShowCommand : DeviceConfigCommand<AppConfigShowCommand>
{
    public AppConfigShowCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation("Reading configuration...");

        var settingsManager = new MeadowSettingsManager(device);
        var settingsDictionary = await settingsManager.ReadAppSettings();

        Logger?.LogInformation("app.config.yaml");

        ShowSettings(Logger, settingsDictionary);
    }
}
