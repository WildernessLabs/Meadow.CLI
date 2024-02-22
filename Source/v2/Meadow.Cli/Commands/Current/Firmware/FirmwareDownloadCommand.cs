using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware download", Description = "Download a firmware package")]
public class FirmwareDownloadCommand : BaseFileCommand<FirmwareDownloadCommand>
{
    private readonly IMeadowCloudClient _meadowCloudClient;

    public FirmwareDownloadCommand(
        FileManager fileManager,
        IMeadowCloudClient meadowCloudClient,
        ISettingsManager settingsManager,
        ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    {
        _meadowCloudClient = meadowCloudClient;
    }

    [CommandOption("force", 'f', IsRequired = false)]
    public bool Force { get; init; }

    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    protected override async ValueTask ExecuteCommand()
    {
        var isAuthenticated = await _meadowCloudClient.Authenticate(CancellationToken);
        if (!isAuthenticated)
        {
            Logger?.LogError($"You must be signed into your Wilderness Labs account to execute this command. Run 'meadow cloud login' to do so.");
            return;
        }

        await FileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = FileManager.Firmware["Meadow F7"];

        bool explicitVersion;

        if (Version == null)
        {
            explicitVersion = false;
            var latest = await collection.GetLatestAvailableVersion();

            if (latest == null)
            {
                Logger?.LogError($"Unable to get latest version information");
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

        var isAvailable = await collection.IsVersionAvailableForDownload(Version);

        if (!isAvailable)
        {
            Logger?.LogError($"Requested package version '{Version}' is not available");
            return;
        }

        if (collection[Version] != null && Force == false)
        {
            Logger?.LogInformation($"Firmware package '{Version}' already exists locally");

            if (explicitVersion == false)
            {
                await collection.SetDefaultPackage(Version);
            }
            return;
        }

        Logger?.LogInformation($"Downloading firmware package '{Version}'...");

        try
        {
            collection.DownloadProgress += OnDownloadProgress;

            var result = await collection.RetrievePackage(Version, Force);

            if (!result)
            {
                Logger?.LogError($"Unable to download package '{Version}'");
            }
            else
            {
                Logger?.LogInformation($"Firmware package '{Version}' downloaded");

                if (explicitVersion == false)
                {
                    await collection.SetDefaultPackage(Version);
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Unable to download package '{Version}': {ex.Message}");
        }
    }

    private void OnDownloadProgress(object? sender, long e)
    {
        // use Console so we can Write instead of Logger which only supports WriteLine
        Console?.Output.Write($"Retrieved {e} bytes...                    \r");
    }
}