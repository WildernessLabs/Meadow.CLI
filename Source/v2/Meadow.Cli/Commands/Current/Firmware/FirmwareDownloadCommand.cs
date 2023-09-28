using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware download", Description = "Download a firmware package")]
public class FirmwareDownloadCommand : BaseFileCommand<FirmwareDownloadCommand>
{
    public FirmwareDownloadCommand(FileManager fileManager, ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    {
    }

    [CommandOption("force", 'f', IsRequired = false)]
    public bool Force { get; set; }

    [CommandParameter(0, Name = "Version number to download", IsRequired = false)]
    public string? Version { get; set; } = default!;

    protected override async ValueTask ExecuteCommand(CancellationToken? cancellationToken)
    {
        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = FileManager.Firmware["Meadow F7"];

        if (Version == null)
        {
            var latest = await collection.GetLatestAvailableVersion();

            if (latest == null)
            {
                Logger?.LogError($"Unable to get latest version information.");
                return;
            }

            Logger?.LogInformation($"Latest available version is '{latest}'...");
            Version = latest;
        }
        else
        {
            Logger?.LogInformation($"Checking for firmware package '{Version}'...");
        }

        var isAvailable = await collection.IsVersionAvailableForDownload(Version);

        if (!isAvailable)
        {
            Logger?.LogError($"Requested package version '{Version}' is not available.");
            return;
        }

        Logger?.LogInformation($"Downloading firmware package '{Version}'...");


        try
        {
            collection.DownloadProgress += OnDownloadProgress;

            var result = await collection.RetrievePackage(Version, Force);

            if (!result)
            {
                Logger?.LogError($"Unable to download package '{Version}'.");
            }
            else
            {
                Logger?.LogError($"{Environment.NewLine} Firmware package '{Version}' downloaded.");
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Unable to download package '{Version}': {ex.Message}");
        }
    }

    private void OnDownloadProgress(object? sender, long e)
    {
        Logger?.LogInformation($"Retrieved {e} bytes...                    \r");
    }
}