using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware delete", Description = "Delete a local firmware package")]
public class FirmwareDeleteCommand : BaseFileCommand<FirmwareDeleteCommand>
{
    public FirmwareDeleteCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    {
    }

    [CommandParameter(0, Name = "Version number to delete", IsRequired = true)]
    public string Version { get; set; } = default!;

    protected override async ValueTask ExecuteCommand()
    {
        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = FileManager.Firmware["Meadow F7"];

        Logger?.LogInformation($"Deleting firmware '{Version}'...");

        await collection.DeletePackage(Version);

        Logger?.LogInformation($"Done.");
    }
}