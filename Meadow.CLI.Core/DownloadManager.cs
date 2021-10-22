using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Meadow.CLI.Core.Common;

using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core
{
    public class DownloadManager
    {
        readonly string _versionCheckUrlRoot =
            "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/";

        public static readonly string FirmwareDownloadsFilePathRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WildernessLabs",
            "Firmware");

        public static string FirmwareLatestVersion {
            get {
                string latest_txt = Path.Combine(FirmwareDownloadsFilePathRoot, "latest.txt");
                if (File.Exists(latest_txt))
                    return File.ReadAllText(latest_txt);
                else
                    throw new FileNotFoundException("OS download was not found.");
             }
        }

        public static string FirmwareDownloadsFilePath {
            get { return Path.Combine(FirmwareDownloadsFilePathRoot, FirmwareLatestVersion); }
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

        public static readonly string UpdateCommand =
            "dotnet tool update WildernessLabs.Meadow.CLI --global";

        private static readonly HttpClient Client = new HttpClient()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        private readonly ILogger _logger;

        public DownloadManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DownloadManager>();
        }

        public async Task DownloadLatestAsync(string? version = null, bool force = false)
        {
            string _versionCheckUrl = null;
            if (version is null) {
                _logger.LogInformation("Downloading latest version file");
                _versionCheckUrl = _versionCheckUrlRoot + "latest.json";
            }
            else {
                _logger.LogInformation("Download version file for release " + version);
                _versionCheckUrl = _versionCheckUrlRoot + version + ".json";
            }
            string VersionCheckFile = new Uri(_versionCheckUrl).Segments.Last();
            var versionCheckFile = await DownloadFileAsync(new Uri(_versionCheckUrl));

            var payload = File.ReadAllText(versionCheckFile);
            var release = JsonSerializer.Deserialize<ReleaseMetadata>(payload);
            if (release == null)
            {
                var ex = new Exception("Unable to identify release.");
                _logger.LogError(ex, "Unable to identify release. Payload: {payload}", payload);
                throw ex;
            }

            File.WriteAllText(Path.Combine(FirmwareDownloadsFilePathRoot, "latest.txt"), release.Version);

            var appVersion = Assembly.GetEntryAssembly()!
                                     .GetCustomAttribute<AssemblyFileVersionAttribute>()
                                     .Version;

            if (release.Version.ToVersion() < "0.6.0.0".ToVersion())
            {
                _logger.LogInformation(
                    $"Installing OS version {release.Version} is not supported by this tool anymore. The minimum version supported is 0.6.0.0.");
                return;
            }

            if (release.MinCLIVersion.ToVersion() > appVersion.ToVersion())
            {
                _logger.LogInformation(
                    $"Installing OS version {release.Version} requires the latest CLI. To update, run: {UpdateCommand}");

                return;
            }

            if (!Directory.Exists(FirmwareDownloadsFilePathRoot))
            {
                Directory.CreateDirectory(FirmwareDownloadsFilePathRoot);
            }

            var local_path = Path.Combine(FirmwareDownloadsFilePathRoot, release.Version);

            if (Directory.Exists(local_path))
            {
                if (force)
                    CleanPath(local_path);
                else {
                     _logger.LogInformation( $"OS version {release.Version} is already installed." );
                     return;
                }
            }

            Directory.CreateDirectory(local_path);

            _logger.LogInformation("Downloading latest MCU firmware");
            await DownloadAndExtractFileAsync(new Uri(release.DownloadURL), local_path);

            _logger.LogInformation("Downloading latest ESP32 firmware");
            await DownloadAndExtractFileAsync(new Uri(release.NetworkDownloadURL), local_path);

            _logger.LogInformation(
                $"Downloaded and extracted OS version {release.Version} to: {local_path}");
        }

        public async Task InstallDfuUtilAsync(bool is64Bit = true,
                                              CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Installing dfu-util...");

                if (Directory.Exists(WildernessLabsTemp))
                {
                    Directory.Delete(WildernessLabsTemp, true);
                }

                Directory.CreateDirectory(WildernessLabsTemp);

                const string downloadUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/public/dfu-util-0.10-binaries.zip";

                var downloadFileName = downloadUrl.Substring(downloadUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
                var response = await Client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                           .ConfigureAwait(false);

                if (response.IsSuccessStatusCode == false)
                    throw new Exception("Failed to download dfu-util");

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var downloadFileStream = new DownloadFileStream(stream, _logger))
                using (var fs = File.OpenWrite(Path.Combine(WildernessLabsTemp, downloadFileName)))
                {
                    await downloadFileStream.CopyToAsync(fs).ConfigureAwait(false);
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

                _logger.LogInformation("dfu-util 0.10 installed");
            }
            catch (Exception ex)
            {
                _logger.LogError(
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

        public async Task<(bool updateExists, string latestVersion, string currentVersion)> CheckForUpdatesAsync()
        {
            try
            {
                var packageId = "WildernessLabs.Meadow.CLI";
                var appVersion = Assembly.GetEntryAssembly()!
                                         .GetCustomAttribute<AssemblyFileVersionAttribute>()
                                         .Version;

                var json = await Client.GetStringAsync(
                               $"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json");

                var result = JsonSerializer.Deserialize<PackageVersions>(json);

                if (!string.IsNullOrEmpty(result?.Versions.LastOrDefault()))
                {
                    var latest = result!.Versions!.Last();
                    return (latest.ToVersion() > appVersion.ToVersion(), latest, appVersion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
            }

            return (false, string.Empty, string.Empty);
        }

        private async Task<string> DownloadFileAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                                     .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var downloadFileName = Path.GetTempFileName();
            _logger.LogDebug("Copying downloaded file to temp file {filename}", downloadFileName);
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var downloadFileStream = new DownloadFileStream(stream, _logger))
            using (var firmwareFile = File.OpenWrite(downloadFileName))
            {
                await downloadFileStream.CopyToAsync(firmwareFile).ConfigureAwait(false);
            }
            return downloadFileName;
        }

        private async Task DownloadAndExtractFileAsync(Uri uri, string target_path, CancellationToken cancellationToken = default)
        {
            var downloadFileName = await DownloadFileAsync(uri, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Extracting firmware to {path}", target_path);
            ZipFile.ExtractToDirectory(
                downloadFileName,
                target_path);
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
                    _logger.LogWarning("Failed to delete file {file} in firmware path", file.FullName);
                    _logger.LogDebug(ex, "Failed to delete file");
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
                    _logger.LogWarning("Failed to delete directory {directory} in firmware path", dir.FullName);
                    _logger.LogDebug(ex, "Failed to delete directory");
                }
            }
        }
    }
}