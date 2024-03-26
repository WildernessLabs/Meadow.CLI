using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("device clock", Description = "Gets or sets the device clock (in UTC time)")]
public class DeviceClockCommand : BaseDeviceCommand<DeviceInfoCommand>
{
    [CommandParameter(0, Name = "Time", IsRequired = false)]
    public string? Time { get; init; }

    public DeviceClockCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        if (Time == null)
        {
            Logger?.LogInformation(Strings.GettingDeviceClock);
            var deviceTime = await device.GetRtcTime(CancellationToken);
            Logger?.LogInformation($"{deviceTime.Value:s}Z");
        }
        else
        {
            if (Time == "now")
            {
                Logger?.LogInformation(Strings.SettingDeviceClock);
                await device.SetRtcTime(DateTimeOffset.UtcNow, CancellationToken);
            }
            else if (DateTimeOffset.TryParse(Time, out DateTimeOffset dto))
            {
                Logger?.LogInformation(Strings.SettingDeviceClock);
                await device.SetRtcTime(dto, CancellationToken);
            }
            else
            {
                throw new CommandException(
                    $"Unable to parse '{Time}' to a valid time.",
                    CommandExitCode.InvalidParameter);
            }
        }
    }
}