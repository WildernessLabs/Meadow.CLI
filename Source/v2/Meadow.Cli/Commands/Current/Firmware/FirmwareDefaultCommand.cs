using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware default", Description = "Sets the current default firmware package")]
public class FirmwareDefaultCommand : BaseFileCommand<FirmwareDefaultCommand>
{
    public FirmwareDefaultCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    {
    }

    [CommandParameter(0, Name = "Version number to use as default", IsRequired = false)]
    public string? Version { get; set; } = null;

    protected override async ValueTask ExecuteCommand()
    {
        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = FileManager.Firmware["Meadow F7"];

        if (Version == null)
        {
            Logger?.LogInformation($"Default firmware is '{collection.DefaultPackage.Version}'.");
        }
        else
        {
            var existing = collection.FirstOrDefault(p => p.Version == Version);

            Logger?.LogInformation($"Setting default firmware to '{Version}'...");

            await collection.SetDefaultPackage(Version);
        }
    }
}