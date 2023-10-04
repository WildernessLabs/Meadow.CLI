using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.Hcom;
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

    [CommandParameter(0, Name = "Version number to use as default", IsRequired = true)]
    public string Version { get; set; } = default!;

    protected override async ValueTask ExecuteCommand()
    {
        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = FileManager.Firmware["Meadow F7"];

        var existing = collection.FirstOrDefault(p => p.Version == Version);

        Logger?.LogInformation($"Setting default firmware to '{Version}'...");

        await collection.SetDefaultPackage(Version);

        Logger?.LogInformation($"Done.");
    }
}