using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace Meadow.Hcom;

public class DownloadManager
{
    public static readonly string FirmwareDownloadsFilePathRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WildernessLabs",
        "Firmware");

    public static string FirmwareLatestVersion
    {
        get
        {
            string latest_txt = Path.Combine(FirmwareDownloadsFilePathRoot, "latest.txt");
            if (File.Exists(latest_txt))
                return File.ReadAllText(latest_txt);
            else
                throw new FileNotFoundException("OS download was not found.");
        }
    }

    public static string FirmwareDownloadsFilePath => FirmwarePathForVersion(FirmwareLatestVersion);

    public static string FirmwarePathForVersion(string firmwareVersion)
    {
        return Path.Combine(FirmwareDownloadsFilePathRoot, firmwareVersion);
    }

    public static readonly string WildernessLabsTemp = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WildernessLabs",
        "temp");

    public static readonly string OsFilename = "Meadow.OS.bin";
    public static readonly string RuntimeFilename = "Meadow.OS.Runtime.bin";
    public static readonly string NetworkBootloaderFilename = "bootloader.bin";
    public static readonly string NetworkMeadowCommsFilename = "MeadowComms.bin";
    public static readonly string NetworkPartitionTableFilename = "partition-table.bin";
    internal static readonly string VersionCheckUrlRoot =
        "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/";

    public static readonly string UpdateCommand = "dotnet tool update WildernessLabs.Meadow.CLI --global";

    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    private readonly ILogger _logger;

    public DownloadManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DownloadManager>();
    }

    public DownloadManager(ILogger logger)
    {
        _logger = logger;
    }

    internal async Task<string?> DownloadMeadowOSVersionFile(string? version)
    {
        string versionCheckUrl;
        if (version is null || string.IsNullOrWhiteSpace(version))
        {
            _logger?.LogInformation("Downloading latest version file" + Environment.NewLine);
            versionCheckUrl = VersionCheckUrlRoot + "latest.json";
        }
        else
        {
            _logger?.LogInformation("Downloading version file for Meadow OS " + version + Environment.NewLine);
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

    //ToDo rename this method - DownloadOSAsync?
    public async Task DownloadOsBinaries(string? version = null, bool force = false)
    {
        var versionCheckFilePath = await DownloadMeadowOSVersionFile(version);

        if (versionCheckFilePath == null)
        {
            _logger?.LogError($"Meadow OS {version} cannot be downloaded or is not available");
            return;
        }

        var payload = File.ReadAllText(versionCheckFilePath);
        var release = JsonSerializer.Deserialize<ReleaseMetadata>(payload);

        if (release == null)
        {
            _logger?.LogError($"Unable to read release details for Meadow OS {version}. Payload: {payload}");
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

        if (release.Version != null && release.Version.ToVersion() < "0.6.0.0".ToVersion())
        {
            _logger?.LogInformation(
                $"Downloading OS version {release.Version} is no longer supported. The minimum OS version is 0.6.0.0." + Environment.NewLine);
            return;
        }

        var local_path = Path.Combine(FirmwareDownloadsFilePathRoot, release.Version);

        if (Directory.Exists(local_path))
        {
            if (force)
            {
                CleanPath(local_path);
            }
            else
            {
                _logger?.LogInformation($"Meadow OS version {release.Version} is already downloaded." + Environment.NewLine);
                return;
            }
        }

        Directory.CreateDirectory(local_path);

        try
        {
            _logger?.LogInformation($"Downloading Meadow OS" + Environment.NewLine);
            await DownloadAndExtractFile(new Uri(release.DownloadURL), local_path);
        }
        catch
        {
            _logger?.LogError($"Unable to download Meadow OS {version}");
            return;
        }

        try
        {
            _logger?.LogInformation("Downloading coprocessor firmware" + Environment.NewLine);
            await DownloadAndExtractFile(new Uri(release.NetworkDownloadURL), local_path);
        }
        catch
        {
            _logger?.LogError($"Unable to download coprocessor firmware {version}");
            return;
        }

        _logger?.LogInformation($"Downloaded and extracted OS version {release.Version} to: {local_path}" + Environment.NewLine);
    }

    public async Task InstallDfuUtil(bool is64Bit = true,
                                          CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Installing dfu-util...");

            if (Directory.Exists(WildernessLabsTemp))
            {
                Directory.Delete(WildernessLabsTemp, true);
            }

            Directory.CreateDirectory(WildernessLabsTemp);

            const string downloadUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/public/dfu-util-0.10-binaries.zip";

            var downloadFileName = downloadUrl.Substring(downloadUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
            var response = await Client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode == false)
            {
                throw new Exception("Failed to download dfu-util");
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var downloadFileStream = new DownloadFileStream(stream, _logger))
            using (var fs = File.OpenWrite(Path.Combine(WildernessLabsTemp, downloadFileName)))
            {
                await downloadFileStream.CopyToAsync(fs);
            }

            ZipFile.ExtractToDirectory(
                Path.Combine(WildernessLabsTemp, downloadFileName),
                WildernessLabsTemp);

            var dfuUtilExe = new FileInfo(
                Path.Combine(WildernessLabsTemp, is64Bit ? "win64" : "win32", "dfu-util.exe"));

            var libUsbDll = new FileInfo(
                Path.Combine(
                    WildernessLabsTemp,
                    is64Bit ? "win64" : "win32",
                    "libusb-1.0.dll"));

            var targetDir = is64Bit
                                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                                : Environment.GetFolderPath(
                                    Environment.SpecialFolder.SystemX86);

            File.Copy(dfuUtilExe.FullName, Path.Combine(targetDir, dfuUtilExe.Name), true);
            File.Copy(libUsbDll.FullName, Path.Combine(targetDir, libUsbDll.Name), true);

            // clean up from previous version
            var dfuPath = Path.Combine(@"C:\Windows\System", dfuUtilExe.Name);
            var libUsbPath = Path.Combine(@"C:\Windows\System", libUsbDll.Name);
            if (File.Exists(dfuPath))
            {
                File.Delete(dfuPath);
            }

            if (File.Exists(libUsbPath))
            {
                File.Delete(libUsbPath);
            }

            _logger?.LogInformation("dfu-util 0.10 installed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                ex.Message.Contains("Access to the path")
                    ? $"Run terminal as administrator and try again."
                    : "Unexpected error");
        }
        finally
        {
            if (Directory.Exists(WildernessLabsTemp))
            {
                Directory.Delete(WildernessLabsTemp, true);
            }
        }
    }

    public async Task<(bool updateExists, string latestVersion, string currentVersion)> CheckForUpdates()
    {
        try
        {
            var packageId = "WildernessLabs.Meadow.CLI";
            var appVersion = Assembly.GetEntryAssembly()!
                                     .GetCustomAttribute<AssemblyFileVersionAttribute>()
                                     .Version;

            var json = await Client.GetStringAsync(
                           $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json");

            var result = JsonSerializer.Deserialize<PackageVersions>(json);

            if (!string.IsNullOrEmpty(result?.Versions.LastOrDefault()))
            {
                var latest = result!.Versions!.Last();
                return (latest.ToVersion() > appVersion.ToVersion(), latest, appVersion);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error checking for updates to Meadow.CLI");
        }

        return (false, string.Empty, string.Empty);
    }

    private async Task<string> DownloadFile(Uri uri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var downloadFileName = Path.GetTempFileName();
        _logger?.LogDebug("Copying downloaded file to temp file {filename}", downloadFileName);
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var downloadFileStream = new DownloadFileStream(stream, _logger))
        using (var firmwareFile = File.OpenWrite(downloadFileName))
        {
            await downloadFileStream.CopyToAsync(firmwareFile);
        }
        return downloadFileName;
    }

    private async Task DownloadAndExtractFile(Uri uri, string target_path, CancellationToken cancellationToken = default)
    {
        var downloadFileName = await DownloadFile(uri, cancellationToken);

        _logger?.LogDebug("Extracting firmware to {path}", target_path);
        ZipFile.ExtractToDirectory(
            downloadFileName,
            target_path);
        try
        {
            File.Delete(downloadFileName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Unable to delete temporary file");
            _logger?.LogDebug(ex, "Unable to delete temporary file");
        }
    }

    private void CleanPath(string path)
    {
        var di = new DirectoryInfo(path);
        foreach (FileInfo file in di.GetFiles())
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to delete file {file} in firmware path", file.FullName);
                _logger?.LogDebug(ex, "Failed to delete file");
            }
        }
        foreach (DirectoryInfo dir in di.GetDirectories())
        {
            try
            {
                dir.Delete(true);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to delete directory {directory} in firmware path", dir.FullName);
                _logger?.LogDebug(ex, "Failed to delete directory");
            }
        }
    }
}
