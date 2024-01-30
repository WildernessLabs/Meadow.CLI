﻿using CliFx.Attributes;
using CliFx.Exceptions;
using Meadow.Cloud.Identity;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("firmware download", Description = "Download a firmware package")]
public class FirmwareDownloadCommand : BaseFileCommand<FirmwareDownloadCommand>
{
    private IdentityManager IdentityManager { get; }

    public FirmwareDownloadCommand(
        FileManager fileManager,
        IdentityManager identityManager, 
        ISettingsManager settingsManager,
        ILoggerFactory loggerFactory)
        : base(fileManager, settingsManager, loggerFactory)
    {
        IdentityManager = identityManager;
    }

    [CommandOption("force", 'f', IsRequired = false)]
    public bool Force { get; init; }

    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
    public string? Host { get; set; }

    protected override async ValueTask ExecuteCommand()
    {
        Host ??= BaseCloudCommand<FirmwareDownloadCommand>.DefaultHost;

        Logger?.LogInformation($"Retrieving firmware information from Meadow.Cloud{(Host != BaseCloudCommand<FirmwareDownloadCommand>.DefaultHost ? $" ({Host.ToLowerInvariant()})" : string.Empty)}...");

        var token = await IdentityManager.GetAccessToken(CancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new CommandException("You must be signed into Meadow.Cloud to execute this command. Run 'meadow cloud login' to do so.");
        }

        FileManager.MeadowCloudClient.BaseAddress = new Uri(Host);
        FileManager.MeadowCloudClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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