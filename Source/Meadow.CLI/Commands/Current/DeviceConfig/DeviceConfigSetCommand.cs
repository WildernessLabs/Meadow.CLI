using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device config set", Description = "Sets meadow.config.yaml setting values")]
public class DeviceConfigSetCommand : DeviceConfigCommand<DeviceConfigSetCommand>
{
    [CommandParameter(0)]
    public string[] Settings { get; set; }

    public DeviceConfigSetCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var keyValuePairs = Settings.Select(s =>
        {
            var parts = s.Split('=');
            return new KeyValuePair<string, string>(parts[0], parts[1]);
        });

        var device = await GetCurrentDevice();

        var wasEnabled = false;

        if (await device.IsRuntimeEnabled())
        {
            wasEnabled = true;
            Logger?.LogInformation("Disabling device runtime...");
            await device.RuntimeDisable();
        }

        Logger?.LogInformation("Sending configuration values to the device...");

        var settingsManager = new MeadowSettingsManager(device);
        await settingsManager.WriteDeviceSetting(keyValuePairs);

        if (wasEnabled)
        {
            // this will reset the device
            Logger?.LogInformation("Enabling device runtime...");
            await device.RuntimeEnable();
        }

        Logger?.LogInformation("Done.");
    }
}
