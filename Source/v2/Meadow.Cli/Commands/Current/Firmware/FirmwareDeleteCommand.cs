using CliFx.Attributes;
using Meadow.CLI;
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
        await base.ExecuteCommand();

        Logger?.LogInformation($"Deleting firmware '{Version}'...");

        if (Collection != null)
            await Collection.DeletePackage(Version);
    }
}