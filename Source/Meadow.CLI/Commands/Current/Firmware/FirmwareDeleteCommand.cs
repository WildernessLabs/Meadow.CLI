using CliFx.Attributes;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware delete", Description = "Delete a local firmware package")]
public class FirmwareDeleteCommand : BaseFileCommand<FirmwareDeleteCommand>
{
    public FirmwareDeleteCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    { }

    [CommandParameter(0, Description = "Version number to delete", IsRequired = true)]
    public string Version { get; init; } = default!;

    protected override async ValueTask ExecuteCommand()
    {
        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = FileManager.Firmware["Meadow F7"];

        bool isDefault = false;

        if (collection.DefaultPackage != null && collection.DefaultPackage.Version == Version)
        {
            isDefault = true;
        }

        Logger?.LogInformation($"Deleting firmware '{Version}'...");

        await collection.DeletePackage(Version);

        if (isDefault)
        {
            if (collection.DefaultPackage == null)
            {
                Logger?.LogInformation("Default firmware deleted - no default package set");
            }
            else
            {
                Logger?.LogInformation($"Default firmware deleted - default package set to {collection.DefaultPackage.Version}");
            }
        }
    }
}