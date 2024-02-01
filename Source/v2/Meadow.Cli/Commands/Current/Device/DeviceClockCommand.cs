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
            return;
        }

        if (Time == null)
        {
            Logger?.LogInformation($"Getting device clock...");
            var deviceTime = await connection.Device.GetRtcTime(CancellationToken);
            Logger?.LogInformation($"{deviceTime.Value:s}Z");
        }
        else
        {
            if (Time == "now")
            {
                Logger?.LogInformation($"Setting device clock...");
                await connection.Device.SetRtcTime(DateTimeOffset.UtcNow, CancellationToken);
            }
            else if (DateTimeOffset.TryParse(Time, out DateTimeOffset dto))
            {
                Logger?.LogInformation($"Setting device clock...");
                await connection.Device.SetRtcTime(dto, CancellationToken);
            }
            else
            {
                Logger?.LogInformation($"Unable to parse '{Time}' to a valid time.");
            }
        }
    }
}