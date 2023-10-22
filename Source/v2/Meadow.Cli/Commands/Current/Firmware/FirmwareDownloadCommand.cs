using CliFx.Attributes;
using Meadow.Cli;
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

    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; } = default!;

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Collection != null)
        {
            bool explicitVersion;

            if (Version == null)
            {
                explicitVersion = false;
                var latest = await Collection.GetLatestAvailableVersion();

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
                explicitVersion = true;
                Logger?.LogInformation($"Checking for firmware package '{Version}'...");
            }

            var isAvailable = await Collection.IsVersionAvailableForDownload(Version);

            if (!isAvailable)
            {
                Logger?.LogError($"Requested package version '{Version}' is not available.");
                return;
            }

            Logger?.LogInformation($"Downloading firmware package '{Version}'...");

            try
            {
                Collection.DownloadProgress += OnDownloadProgress;

                var result = await Collection.RetrievePackage(Version, Force);

                if (!result)
                {
                    Logger?.LogError($"Unable to download package '{Version}'.");
                }
                else
                {
                    Logger?.LogError($"{Environment.NewLine} Firmware package '{Version}' downloaded.");

                    if (!explicitVersion)
                    {
                        await Collection.SetDefaultPackage(Version);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Unable to download package '{Version}': {ex.Message}");
            }
        }
    }

    // TODO private long _lastProgress = 0;

    private void OnDownloadProgress(object? sender, long e)
    {
        // use Console so we can Write instead of Logger which only supports WriteLine
        Console?.Output.Write($"Retrieved {e} bytes...                    \r");
    }
}