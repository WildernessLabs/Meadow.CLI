using CliFx.Attributes;
using Meadow.Cloud.Identity;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("download os", Description = "** Deprecated ** Use `firmware download` instead")]
public class DownloadOsCommand : FirmwareDownloadCommand
{
    public DownloadOsCommand(FileManager fileManager, IdentityManager identityManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, identityManager, settingsManager, loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command - use `firmware download` instead");
    }
}