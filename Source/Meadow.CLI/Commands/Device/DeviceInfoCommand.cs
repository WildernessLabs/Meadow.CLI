using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device info", Description = "Get the device info")]
public class DeviceInfoCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    [CommandOption("public-key", 'k', Description = "Include the target device's public key in the output", IsRequired = false)]
    public bool GetKey { get; set; } = false;

    public DeviceInfoCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation($"{Strings.GettingDeviceInfo}...");

        var deviceInfo = await device.GetDeviceInfo(CancellationToken);

        if (deviceInfo != null)
        {
            Logger?.LogInformation(deviceInfo.ToString());
        }

        if (GetKey)
        {
            Logger?.LogInformation($"{Strings.GettingDevicePublicKey}...");

            var publicKey = await device.GetPublicKey(CancellationToken);

            if (!string.IsNullOrWhiteSpace(publicKey))
            {
                Logger?.LogInformation($"{publicKey}");
            }
        }
    }
}