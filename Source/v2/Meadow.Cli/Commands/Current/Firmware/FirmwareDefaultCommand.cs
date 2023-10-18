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
        await base.ExecuteCommand();

        if (Collection != null)
        {
            if (Version == null)
            {
                Logger?.LogInformation($"Default firmware is '{Collection.DefaultPackage?.Version}'.");
            }
            else
            {
                var existing = Collection.FirstOrDefault(p => p.Version == Version);

                Logger?.LogInformation($"Setting default firmware to '{Version}'...");

                await Collection.SetDefaultPackage(Version);
            }
        }
    }
}