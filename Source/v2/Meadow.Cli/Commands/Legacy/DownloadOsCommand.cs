using CliFx.Attributes;
using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("download os", Description = "** Deprecated ** Use `firmware download` instead")]
public class DownloadOsCommand : FirmwareDownloadCommand
{
    public DownloadOsCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command.  Use `runtime disable` instead");
    }
}
