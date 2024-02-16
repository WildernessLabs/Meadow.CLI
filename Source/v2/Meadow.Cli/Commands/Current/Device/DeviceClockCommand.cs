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
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            throw CommandException.MeadowDeviceNotFound;
        }

        if (Time == null)
        {
            Logger?.LogInformation(Strings.GettingDeviceClock);
            var deviceTime = await connection.Device.GetRtcTime(CancellationToken);
            Logger?.LogInformation($"{deviceTime.Value:s}Z");
        }
        else
        {
            if (Time == "now")
            {
                Logger?.LogInformation(Strings.SettingDeviceClock);
                await connection.Device.SetRtcTime(DateTimeOffset.UtcNow, CancellationToken);
            }
            else if (DateTimeOffset.TryParse(Time, out DateTimeOffset dto))
            {
                Logger?.LogInformation(Strings.SettingDeviceClock);
                await connection.Device.SetRtcTime(dto, CancellationToken);
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