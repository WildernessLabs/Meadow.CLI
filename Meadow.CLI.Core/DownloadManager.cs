using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public class DownloadManager
    {
        readonly string _versionCheckUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/latest.json";
        string VersionCheckFile => new Uri(_versionCheckUrl).Segments.Last();

        public static readonly string FirmwareDownloadsFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "Firmware");
        public static readonly string WildernessLabsTemp =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "temp");
        public static readonly string OSFilename = "Meadow.OS.bin";
        public static readonly string RuntimeFilename = "Meadow.OS.Runtime.bin";
        public static readonly string NetworkBootloaderFilename = "bootloader.bin";
        public static readonly string NetworkMeadowCommsFilename = "MeadowComms.bin";
        public static readonly string NetworkPartitionTableFilename = "partition-table.bin";
        public static readonly string UpdateCommand = "dotnet tool update WildernessLabs.Meadow.CLI --global";
        private static readonly HttpClient Client = new HttpClient();

        public async Task DownloadLatest()
        {
            var payload = await Client.GetStringAsync(_versionCheckUrl);
            var release = JsonSerializer.Deserialize<ReleaseMetadata>(payload);
            var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

            if (release.MinCLIVersion.ToVersion() > appVersion.ToVersion())
            {
                Console.WriteLine($"Installing OS version {release.Version} requires the latest CLI. To update, run: {UpdateCommand}");
                return;
            }

            if (Directory.Exists(FirmwareDownloadsFilePath))
            {
                Directory.Delete(FirmwareDownloadsFilePath, true);
            }
            Directory.CreateDirectory(FirmwareDownloadsFilePath);

            await DownloadFile(new Uri(release.DownloadURL));
            await DownloadFile(new Uri(release.NetworkDownloadURL));

            Console.WriteLine($"Download and extracted OS version {release.Version} to:\r\n{FirmwareDownloadsFilePath}");
        }

        public async Task InstallDfuUtil(bool is64bit = true)
        {
            try
            {
                Console.WriteLine("Installing dfu-util...");

                if (Directory.Exists(WildernessLabsTemp))
                {
                    Directory.Delete(WildernessLabsTemp, true);
                }
                Directory.CreateDirectory(WildernessLabsTemp);

                var downloadUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/public/dfu-util-0.10-binaries.zip";
                var downloadFileName = downloadUrl.Substring(downloadUrl.LastIndexOf("/") + 1);
                var response = await Client.GetAsync(downloadUrl)
                      .ConfigureAwait(false);

                if (response.IsSuccessStatusCode == false)
                    throw new Exception("Failed to download dfu-util");

                using var fs =
                    File.OpenWrite(Path.Combine(WildernessLabsTemp, downloadFileName));
                await response.Content.CopyToAsync(fs)
                              .ConfigureAwait(false);

                ZipFile.ExtractToDirectory(Path.Combine(WildernessLabsTemp, downloadFileName), WildernessLabsTemp);

                var dfuutilexe = new FileInfo(Path.Combine(WildernessLabsTemp, is64bit ? "win64" : "win32", "dfu-util.exe"));
                var libusbdll = new FileInfo(Path.Combine(WildernessLabsTemp, is64bit ? "win64" : "win32", "libusb-1.0.dll"));

                var targetDir = is64bit ?
                    Environment.GetFolderPath(Environment.SpecialFolder.System) :
                    Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

                File.Copy(dfuutilexe.FullName, Path.Combine(targetDir, dfuutilexe.Name), true);
                File.Copy(libusbdll.FullName, Path.Combine(targetDir, libusbdll.Name), true);

                // clean up from previous version
                if (File.Exists(Path.Combine(@"C:\Windows\System", dfuutilexe.Name)))
                {
                    File.Delete(Path.Combine(@"C:\Windows\System", dfuutilexe.Name));
                }
                if (File.Exists(Path.Combine(@"C:\Windows\System", libusbdll.Name)))
                {
                    File.Delete(Path.Combine(@"C:\Windows\System", libusbdll.Name));
                }
                Console.WriteLine("dfu-util 0.10 installed");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Access to the path"))
                {
                    Console.WriteLine($"{ex.Message}{Environment.NewLine}Run terminal as administrator and try again.");
                }
                else
                {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                }
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
                var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
                var json = await Client.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json");
                var result = JsonSerializer.Deserialize<PackageVersions>(json);

                if (!string.IsNullOrEmpty(result?.versions?.LastOrDefault()))
                {
                    var latest = result.versions.Last();
                    return (latest.ToVersion() > appVersion.ToVersion(), latest, appVersion);
                }
            }
            catch (Exception ex)
            {
            }
            return (false, string.Empty, string.Empty);
        }

        async Task DownloadFile(Uri uri, CancellationToken cancellationToken = default)
        {
            var fileName = uri.Segments.ToList().Last();

            using var firmwareRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            using var firmwareResponse = await Client.SendAsync(firmwareRequest, cancellationToken)
                        .ConfigureAwait(false);

            firmwareResponse.EnsureSuccessStatusCode();
            using var firmwareFile = File.OpenWrite(Path.Combine(FirmwareDownloadsFilePath, fileName));
            await firmwareResponse.Content.CopyToAsync(firmwareFile).ConfigureAwait(false);

            using var versionRequest = new HttpRequestMessage(HttpMethod.Get, _versionCheckUrl);
            using var versionResponse = await Client.SendAsync(versionRequest, cancellationToken)
                                                    .ConfigureAwait(false);

            versionResponse.EnsureSuccessStatusCode();
            var versionFile = File.OpenWrite(Path.Combine(FirmwareDownloadsFilePath, VersionCheckFile));
            await versionResponse.Content.CopyToAsync(versionFile).ConfigureAwait(false);

            ZipFile.ExtractToDirectory(Path.Combine(FirmwareDownloadsFilePath, fileName), FirmwareDownloadsFilePath);
        }
    }
}
