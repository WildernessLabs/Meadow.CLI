using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.Cli;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware download", Description = "Download a firmware package")]
public class FirmwareDownloadCommand : ICommand
{
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<DeviceInfoCommand>? _logger;

    public FirmwareDownloadCommand(ISettingsManager settingsManager, ILoggerFactory? loggerFactory)
    {
        _settingsManager = settingsManager;
        _logger = loggerFactory?.CreateLogger<DeviceInfoCommand>();
    }

    [CommandOption("force", 'f', IsRequired = false)]
    public bool Force { get; set; }

    [CommandParameter(0, Name = "Version number to download", IsRequired = false)]
    public string? Version { get; set; } = default!;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var manager = new FileManager();

        await manager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = manager.Firmware["Meadow F7"];

        if (Version == null)
        {
            var latest = await collection.GetLatestAvailableVersion();

            if (latest == null)
            {
                _logger?.LogError($"Unable to get latest version information.");
                return;
            }

            _logger?.LogInformation($"Latest available version is '{latest}'...");
            Version = latest;
        }
        else
        {
            _logger?.LogInformation($"Checking for firmware package '{Version}'...");
        }

        var isAvailable = await collection.IsVersionAvailableForDownload(Version);

        if (!isAvailable)
        {
            _logger?.LogError($"Requested package version '{Version}' is not available.");
            return;
        }

        _logger?.LogInformation($"Downloading firmware package '{Version}'...");


        try
        {
            collection.DownloadProgress += OnDownloadProgress;

            var result = await collection.RetrievePackage(Version, Force);

            if (!result)
            {
                _logger?.LogError($"Unable to download package '{Version}'.");
            }
            else
            {
                _logger?.LogError($"{Environment.NewLine} Firmware package '{Version}' downloaded.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Unable to download package '{Version}': {ex.Message}");
        }
    }

    private void OnDownloadProgress(object? sender, long e)
    {
        Console.Write($"Retrieved {e} bytes...                    \r");
    }
}
