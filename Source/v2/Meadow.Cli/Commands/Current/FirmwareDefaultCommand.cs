using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware default", Description = "Sets the current default firmware package")]
public class FirmwareDefaultCommand : BaseCommand<FirmwareDefaultCommand>
{
    public FirmwareDefaultCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
    }

    [CommandParameter(0, Name = "Version number to use as default", IsRequired = true)]
    public string Version { get; set; } = default!;

    protected override async ValueTask ExecuteCommand(CancellationToken cancellationToken)
    {
        var manager = new FileManager();

        await manager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = manager.Firmware["Meadow F7"];

        var existing = collection.FirstOrDefault(p => p.Version == Version);

        Logger?.LogInformation($"Setting default firmware to '{Version}'...");

        await collection.SetDefaultPackage(Version);

        Logger?.LogInformation($"Done.");
    }
}
