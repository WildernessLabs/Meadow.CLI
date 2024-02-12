using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("download os", Description = "** Deprecated ** Use `firmware download` instead")]
public class DownloadOsCommand : FirmwareDownloadCommand
{
    public DownloadOsCommand(FileManager fileManager, IMeadowCloudClient meadowCloudClient, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, meadowCloudClient, settingsManager, loggerFactory)
    {
        Logger?.LogWarning($"Deprecated command - use `firmware download` instead");
    }
}