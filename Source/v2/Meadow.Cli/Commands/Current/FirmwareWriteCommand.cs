using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public enum FirmwareType
{
    OS,
    Runtime,
    ESP
}

[Command("firmware write", Description = "Download a firmware package")]
public class FirmwareWriteCommand : BaseDeviceCommand<FirmwareWriteCommand>
{
    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    [CommandParameter(0, Name = "Files to write", IsRequired = false)]
    public FirmwareType[]? Files { get; set; } = default!;

    public FirmwareWriteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override ValueTask ExecuteCommand(Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        if (Files == null)
        {
            Logger.LogInformation("Writing all firmware...");
        }
        else
        {
            if (Files.Contains(FirmwareType.OS))
            {
                Logger.LogInformation("Writing OS...");
            }
            if (Files.Contains(FirmwareType.Runtime))
            {
                Logger.LogInformation("Writing Runtime...");
            }
            if (Files.Contains(FirmwareType.ESP))
            {
                Logger.LogInformation("Writing ESP...");
            }
        }

        return ValueTask.CompletedTask;
    }
}

