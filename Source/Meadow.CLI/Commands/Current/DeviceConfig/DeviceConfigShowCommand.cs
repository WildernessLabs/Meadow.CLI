using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device config show", Description = "Shows the connected device's meadow.config.yaml settings")]
public class DeviceConfigShowCommand : DeviceConfigCommand<DeviceConfigShowCommand>
{
    public DeviceConfigShowCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation("Reading configuration...");

        var settingsManager = new MeadowSettingsManager(device);
        var settingsDictionary = await settingsManager.ReadDeviceSettings();

        Logger?.LogInformation("meadow.config.yaml");

        ShowSettings(Logger, settingsDictionary);
    }
}
