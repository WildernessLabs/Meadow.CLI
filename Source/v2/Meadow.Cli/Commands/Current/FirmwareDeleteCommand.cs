using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware delete", Description = "Delete a local firmware package")]
public class FirmwareDeleteCommand : BaseCommand<FirmwareDeleteCommand>
{
    public FirmwareDeleteCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
    }

    [CommandParameter(0, Name = "Version number to delete", IsRequired = true)]
    public string Version { get; set; } = default!;

    protected override async ValueTask ExecuteCommand(CancellationToken cancellationToken)
    {
        var manager = new FileManager();

        await manager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = manager.Firmware["Meadow F7"];

        var existing = collection.FirstOrDefault(p => p.Version == Version);

        if (existing == null)
        {
        }

        Logger?.LogInformation($"Deleting firmware '{Version}'...");

        await collection.DeletePackage(Version);

        Logger?.LogInformation($"Done.");
    }
}
