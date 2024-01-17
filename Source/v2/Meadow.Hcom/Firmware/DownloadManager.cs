using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text.Json;

namespace Meadow.Hcom;

public class DownloadManager
{
    static readonly string RootFolder = "WildernessLabs";
    static readonly string FirmwareFolder = "Firmware";
    static readonly string LatestFilename = "latest.txt";

    public static readonly string OsFilename = "Meadow.OS.bin";
    public static readonly string RuntimeFilename = "Meadow.OS.Runtime.bin";
    public static readonly string NetworkBootloaderFilename = "bootloader.bin";
    public static readonly string NetworkMeadowCommsFilename = "MeadowComms.bin";
    public static readonly string NetworkPartitionTableFilename = "partition-table.bin";
    internal static readonly string VersionCheckUrlRoot =
        "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/";

    public static readonly string UpdateCommand = "dotnet tool update WildernessLabs.Meadow.CLI --global";

    public static readonly string FirmwareDownloadsFilePathRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        RootFolder, FirmwareFolder);

    public static string FirmwareLatestVersion
    {
        get
        {
            string latestPath = Path.Combine(FirmwareDownloadsFilePathRoot, LatestFilename);
            if (File.Exists(latestPath))
            {
                return File.ReadAllText(latestPath);
            }
            throw new FileNotFoundException("Latest firmware not found");
        }
    }

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(1) };

    private readonly ILogger _logger;

    public DownloadManager(ILogger logger)
    {
        _logger = logger;
    }

    internal async Task<string?> DownloadMeadowOSVersionFile(string? version)
    {
        string versionCheckUrl;
        if (version is null || string.IsNullOrWhiteSpace(version))
        {
            _logger.LogInformation("Downloading latest version file" + Environment.NewLine);
            versionCheckUrl = VersionCheckUrlRoot + "latest.json";
        }
        else
        {
            _logger.LogInformation("Downloading version file for Meadow OS " + version + Environment.NewLine);
            versionCheckUrl = VersionCheckUrlRoot + version + ".json";
        }

        string versionCheckFile;

        try
        {
            versionCheckFile = await DownloadFile(new Uri(versionCheckUrl));
        }
        catch
        {
            return null;
        }

        return versionCheckFile;
    }

    public async Task DownloadOsBinaries(string? version = null, bool force = false)
    {
        var versionCheckFilePath = await DownloadMeadowOSVersionFile(version);

        if (versionCheckFilePath == null)
        {
            _logger.LogError($"Meadow OS {version} cannot be downloaded or is not available");
            return;
        }

        var payload = File.ReadAllText(versionCheckFilePath);
        var release = JsonSerializer.Deserialize<ReleaseMetadata>(payload);

        if (release == null)
        {
            _logger.LogError($"Unable to read release details for Meadow OS {version}. Payload: {payload}");
            return;
        }

        if (!Directory.Exists(FirmwareDownloadsFilePathRoot))
        {
            Directory.CreateDirectory(FirmwareDownloadsFilePathRoot);
            //we'll write latest.txt regardless of version if it doesn't exist
            File.WriteAllText(Path.Combine(FirmwareDownloadsFilePathRoot, "latest.txt"), release.Version);
        }
        else if (version == null)
        {   //otherwise only update if we're pulling the latest release OS
            File.WriteAllText(Path.Combine(FirmwareDownloadsFilePathRoot, "latest.txt"), release.Version);
        }

        if (release.Version.ToVersion() < "0.6.0.0".ToVersion())
        {
            _logger.LogInformation(
                $"Downloading OS version {release.Version} is no longer supported. The minimum OS version is 0.6.0.0." + Environment.NewLine);
            return;
        }

        var local_path = Path.Combine(FirmwareDownloadsFilePathRoot, release.Version);

        if (Directory.Exists(local_path))
        {
            if (force)
            {
                DeleteDirectoryContents(local_path);
            }
            else
            {
                _logger.LogInformation($"Meadow OS version {release.Version} is already downloaded." + Environment.NewLine);
                return;
            }
        }

        Directory.CreateDirectory(local_path);

        try
        {
            _logger.LogInformation($"Downloading Meadow OS" + Environment.NewLine);
            await DownloadAndExtractFile(new Uri(release.DownloadURL), local_path);
        }
        catch
        {
            _logger.LogError($"Unable to download Meadow OS {version}");
            return;
        }

        try
        {
            _logger.LogInformation("Downloading coprocessor firmware" + Environment.NewLine);
            await DownloadAndExtractFile(new Uri(release.NetworkDownloadURL), local_path);
        }
        catch
        {
            _logger.LogError($"Unable to download coprocessor firmware {version}");
            return;
        }

        _logger.LogInformation($"Downloaded and extracted OS version {release.Version} to: {local_path}" + Environment.NewLine);
    }

    private async Task<string> DownloadFile(Uri uri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var downloadFileName = Path.GetTempFileName();
        _logger.LogDebug("Copying downloaded file to temp file {filename}", downloadFileName);

        using var stream = await response.Content.ReadAsStreamAsync();
        using var downloadFileStream = new DownloadFileStream(stream, _logger);
        using var firmwareFile = File.OpenWrite(downloadFileName);

        await downloadFileStream.CopyToAsync(firmwareFile);

        return downloadFileName;
    }

    private async Task DownloadAndExtractFile(Uri uri, string targetPath, CancellationToken cancellationToken = default)
    {
        var downloadFileName = await DownloadFile(uri, cancellationToken);

        _logger.LogDebug($"Extracting firmware to {targetPath}");
        ZipFile.ExtractToDirectory(
            downloadFileName,
            targetPath);
        try
        {
            File.Delete(downloadFileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to delete temporary file");
            _logger.LogDebug(ex, "Unable to delete temporary file");
        }
    }

    private void DeleteDirectoryContents(string path)
    {
        var di = new DirectoryInfo(path);
        foreach (var fileSystemInfo in di.GetFileSystemInfos())
        {
            try
            {
                if (fileSystemInfo is FileInfo file)
                {
                    file.Delete();
                }
                else if (fileSystemInfo is DirectoryInfo dir)
                {
                    dir.Delete(true);
                }
            }
            catch (Exception ex)
            {
                var type = fileSystemInfo is FileInfo ? "file" : "directory";
                _logger.LogWarning("Failed to delete {type} {path} in firmware path", type, fileSystemInfo.FullName);
                _logger.LogDebug(ex, "Failed to delete {type}", type);
            }
        }
    }
}