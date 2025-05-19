using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device config wifi", Description = "Configure the connected device's wifi settings")]
public class DeviceConfigWifiCommand : BaseDeviceCommand<DeviceConfigWifiCommand>
{
    [CommandOption("ssid", 's', IsRequired = true)]
    public string Ssid { get; init; }

    [CommandOption("passcode", 'p', IsRequired = true)]
    public string Passcode { get; init; }


    public DeviceConfigWifiCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        var settingsManager = new MeadowSettingsManager(device);
        var wasEnabled = false;

        if (await device.IsRuntimeEnabled())
        {
            wasEnabled = true;
            Logger?.LogInformation("Disabling device runtime...");
            await device.RuntimeDisable();
        }

        Logger?.LogInformation("Sending WiFi settings to the device...");
        await settingsManager.WriteWiFiSettings(Ssid, Passcode);

        if (wasEnabled)
        {
            // this will reset the device
            Logger?.LogInformation("Enabling device runtime...");
            await device.RuntimeEnable();
        }
        else
        {
            Logger?.LogInformation("Resetting the device...");
            await device.Reset();
        }
        Logger?.LogInformation("Done.");
    }
}
