using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Logging;

namespace Meadow.CLI.Core
{
    public class DownloadManager
    {
        readonly string _versionCheckUrl =
            "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/latest.json";

        string VersionCheckFile => new Uri(_versionCheckUrl).Segments.Last();

        public static readonly string FirmwareDownloadsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WildernessLabs",
            "Firmware");

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

        private static readonly HttpClient Client = new HttpClient();
        private readonly IMeadowLogger _logger;

        public DownloadManager(IMeadowLogger logger)
        {
            _logger = logger;
        }

        public async Task DownloadLatestAsync()
        {
            var payload = await Client.GetStringAsync(_versionCheckUrl);
            var release = JsonSerializer.Deserialize<ReleaseMetadata>(payload);
            if (release == null)
            {
                var ex = new Exception("Unable to identify release.");
                _logger.LogError(ex, "Unable to identify release. Payload: {payload}", payload);
                throw ex;
            }

            var appVersion = Assembly.GetEntryAssembly()!
                                     .GetCustomAttribute<AssemblyFileVersionAttribute>()
                                     .Version;

            if (release.MinCLIVersion.ToVersion() > appVersion.ToVersion())
            {
                _logger.LogInformation(
                    $"Installing OS version {release.Version} requires the latest CLI. To update, run: {UpdateCommand}");

                return;
            }

            if (Directory.Exists(FirmwareDownloadsFilePath))
            {
                Directory.Delete(FirmwareDownloadsFilePath, true);
            }

            Directory.CreateDirectory(FirmwareDownloadsFilePath);

            await DownloadFile(new Uri(release.DownloadURL));
            await DownloadFile(new Uri(release.NetworkDownloadURL));

            _logger.LogInformation(
                $"Download and extracted OS version {release.Version} to: {FirmwareDownloadsFilePath}");
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
                var response = await Client.GetAsync(downloadUrl, cancellationToken)
                                           .ConfigureAwait(false);

                if (response.IsSuccessStatusCode == false)
                    throw new Exception("Failed to download dfu-util");

                using var fs = File.OpenWrite(Path.Combine(WildernessLabsTemp, downloadFileName));
                await response.Content.CopyToAsync(fs)
                              .ConfigureAwait(false);

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
                File.Copy(libUsbDll.FullName,  Path.Combine(targetDir, libUsbDll.Name),  true);

                // clean up from previous version
                var dfuPath = Path.Combine(@"C:\Windows\System",    dfuUtilExe.Name);
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

        public async Task<(bool updateExists, string latestVersion, string currentVersion)>
            CheckForUpdatesAsync()
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

        private async Task DownloadFile(Uri uri, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Downloading latest firmware");
            using var firmwareRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            using var firmwareResponse = await Client.SendAsync(firmwareRequest, cancellationToken)
                                                     .ConfigureAwait(false);

            firmwareResponse.EnsureSuccessStatusCode();
            
            var downloadFileName = Path.GetTempFileName();
            _logger.LogDebug("Copying firmware to temp file {filename}", downloadFileName);
            using (var firmwareFile = File.OpenWrite(downloadFileName))
            {
                await firmwareResponse.Content.CopyToAsync(firmwareFile)
                                      .ConfigureAwait(false);
            }

            _logger.LogDebug("Downloading latest version file");
            using var versionRequest = new HttpRequestMessage(HttpMethod.Get, _versionCheckUrl);
            using var versionResponse = await Client.SendAsync(versionRequest, cancellationToken)
                                                    .ConfigureAwait(false);

            versionResponse.EnsureSuccessStatusCode();

            var versionFileName = Path.Combine(FirmwareDownloadsFilePath, VersionCheckFile);
            
            _logger.LogDebug("Copying version file to {filename}", versionFileName);
            using (var versionFile =
                File.OpenWrite(versionFileName))
            {

                await versionResponse.Content.CopyToAsync(versionFile)
                                     .ConfigureAwait(false);
            }

            _logger.LogDebug("Extracting firmware to {path}", FirmwareDownloadsFilePath);
            ZipFile.ExtractToDirectory(
                downloadFileName,
                FirmwareDownloadsFilePath);
        }
    }
}