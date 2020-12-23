using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;
using Meadow.CLI.Core;

namespace Meadow.CLI
{
    public class DownloadManager
    {
        readonly string _versionCheckUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/latest.json";
        string _versionCheckFile { get { return new Uri(_versionCheckUrl).Segments.Last(); } }

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

        public async Task DownloadLatest()
        {
            HttpClient httpClient = new HttpClient();
            var payload = await httpClient.GetStringAsync(_versionCheckUrl);
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

        public void InstallDfuUtil(bool is64bit = true)
        {
            try
            {
                if (Directory.Exists(WildernessLabsTemp))
                {
                    Directory.Delete(WildernessLabsTemp, true);
                }
                Directory.CreateDirectory(WildernessLabsTemp);

                WebClient client = new WebClient();
                var downloadUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/public/dfu-util-0.10-binaries.zip";
                var downloadFileName = downloadUrl.Substring(downloadUrl.LastIndexOf("/") + 1);
                client.DownloadFile(downloadUrl, Path.Combine(WildernessLabsTemp, downloadFileName));
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
            catch(Exception ex)
            {
                if(ex.Message.Contains("Access to the path"))
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

        public async Task<(bool updateExists, string latestVersion)> CheckForUpdates()
        {
            try
            {
                var packageId = "WildernessLabs.Meadow.CLI";
                var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
                HttpClient client = new HttpClient();
                var json = await client.GetStringAsync($"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json");
                var result = JsonSerializer.Deserialize<PackageVersions>(json);

                if (!string.IsNullOrEmpty(result?.versions?.LastOrDefault()))
                {
                    var latest = result.versions.Last();
                    return (latest.ToVersion() > appVersion.ToVersion(), latest);
                }
            }
            catch(Exception ex)
            {
            }
            return (false, string.Empty);
        }

        async Task DownloadFile(Uri uri)
        {
            var fileName = uri.Segments.ToList().Last();

            WebClient webClient = new WebClient();
            webClient.DownloadFile(uri, Path.Combine(FirmwareDownloadsFilePath, fileName));
            webClient.DownloadFile(_versionCheckUrl, Path.Combine(FirmwareDownloadsFilePath, _versionCheckFile));
            ZipFile.ExtractToDirectory(Path.Combine(FirmwareDownloadsFilePath, fileName), FirmwareDownloadsFilePath);
        }
    }
}
