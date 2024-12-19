using CliFx.Attributes;
using Meadow.CLI.Commands.Current.Firmware;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware write", Description = "Writes firmware files to a connected meadow device")]
public class FirmwareWriteCommand : BaseDeviceCommand<FirmwareWriteCommand>
{
    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    [CommandOption("use-dfu", 'd', IsRequired = false, Description = "Force using DFU/HCOM for writing files")]
    public bool UseDfu { get; set; } = true;

    [CommandOption("file", 'f', IsRequired = false, Description = "Send only the specified file")]
    public string? IndividualFile { get; set; }

    [CommandParameter(0, Description = "Files to write", IsRequired = false)]
    public FirmwareType[]? FirmwareFileTypes { get; set; } = default!;

    [CommandOption("serialnumber", 's', IsRequired = false, Description = "Flash the specified device")]
    public string? SerialNumber { get; set; }

    private FileManager FileManager { get; }
    private ISettingsManager Settings { get; }

    private string DFU_USB_ERROR_MESSAGE = "Operation not supported or unimplemented on this platform";

    public FirmwareWriteCommand(ISettingsManager settingsManager, FileManager fileManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        FileManager = fileManager;
        Settings = settingsManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var firmareUpdater = new FirmwareUpdater<FirmwareWriteCommand>(this, Settings, FileManager, ConnectionManager, IndividualFile, FirmwareFileTypes, UseDfu, Version, SerialNumber, Logger, CancellationToken);

        if (await firmareUpdater.UpdateFirmware())
        {
            Logger?.LogInformation(Strings.FirmwareUpdatedSuccessfully);
        }
    }
}