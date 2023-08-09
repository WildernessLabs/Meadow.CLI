using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device clock", Description = "Gets or sets the device clock (in UTC time)")]
public class DeviceClockCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    [CommandParameter(0, Name = "Time", IsRequired = false)]
    public string? Time { get; set; }

    public DeviceClockCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        if (Time == null)
        {
            Logger.LogInformation($"Getting device clock...");
            var deviceTime = await device.GetRtcTime(cancellationToken);
            Logger.LogInformation($"{deviceTime.Value:s}Z");
        }
        else
        {
            if (Time == "now")
            {
                Logger.LogInformation($"Setting device clock...");
                await device.SetRtcTime(DateTimeOffset.UtcNow, cancellationToken);
            }
            else if (DateTimeOffset.TryParse(Time, out DateTimeOffset dto))
            {
                Logger.LogInformation($"Setting device clock...");
                await device.SetRtcTime(dto, cancellationToken);
            }
            else
            {
                Logger.LogInformation($"Unable to parse '{Time}' to a valid time.");
            }
        }
    }
}

[Command("device info", Description = "Get the device info")]
public class DeviceInfoCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    public DeviceInfoCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        Logger.LogInformation($"Getting device info...");
    }

    protected override async ValueTask ExecuteCommand(Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        var deviceInfo = await device.GetDeviceInfo(cancellationToken);
        if (deviceInfo != null)
        {
            Logger.LogInformation(deviceInfo.ToString());
        }
    }
}
