﻿using CliFx.Attributes;
using Meadow.Cloud.Client;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware download", Description = "Download a firmware package")]
public class FirmwareDownloadCommand : BaseCloudCommand<FirmwareDownloadCommand>
{
    private readonly FileManager _fileManager;

    public FirmwareDownloadCommand(
        FileManager fileManager,
        IMeadowCloudClient meadowCloudClient,
        ILoggerFactory loggerFactory)
        : base(meadowCloudClient, loggerFactory)
    {
        _fileManager = fileManager;
        RequiresAuthentication = false;
    }

    [CommandOption("force", 'f', IsRequired = false)]
    public bool Force { get; init; }

    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    protected override async ValueTask ExecuteCloudCommand()
    {
        await _fileManager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = _fileManager.Firmware["Meadow F7"];

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

            Logger.LogInformation($"Latest available version is '{latest}'...");
            Version = latest;
        }
        else
        {
            explicitVersion = true;
            Logger.LogInformation($"Checking for firmware package '{Version}'...");
        }

        var isAvailable = await collection.IsVersionAvailableForDownload(Version);

        if (!isAvailable)
        {
            Logger.LogError($"Requested package version '{Version}' is not available");
            return;
        }

        if (collection[Version] != null && Force == false)
        {
            Logger.LogInformation($"Firmware package '{Version}' already exists locally");

            if (explicitVersion == false)
            {
                await collection.SetDefaultPackage(Version);
            }
            return;
        }

        Logger.LogInformation($"Downloading firmware package '{Version}'...");

        try
        {
            collection.DownloadProgress += OnDownloadProgress;

            var result = await collection.RetrievePackage(Version, Force);

            if (!result)
            {
                Logger.LogError($"Unable to download package '{Version}'");
            }
            else
            {
                Logger.LogInformation($"Firmware package '{Version}' downloaded to {collection.PackageFileRoot}");

                if (explicitVersion == false)
                {
                    await collection.SetDefaultPackage(Version);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"{ex.Message}");
        }
    }

    private void OnDownloadProgress(object? sender, long e)
    {
        // use Console so we can Write instead of Logger which only supports WriteLine
        Console.Output.Write($"Retrieved {e} bytes...                    \r");
    }
}