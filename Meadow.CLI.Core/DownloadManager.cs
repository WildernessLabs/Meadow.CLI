using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;

namespace Meadow.CLI
{
    public class DownloadManager
    {
        readonly string _versionCheckUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/latest.json";
        string _versionCheckFile { get { return new Uri(_versionCheckUrl).Segments.Last(); } }

        public readonly string FirmwareDownloadsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "Firmware");
        public readonly string WildernessLabsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "temp");
        public readonly string OSFilename = "Meadow.OS.bin";
        public readonly string RuntimeFilename = "Meadow.OS.Runtime.bin";
        public readonly string NetworkBootloaderFilename = "bootloader.bin";
        public readonly string NetworkMeadowCommsFilename = "MeadowComms.bin";
        public readonly string NetworkPartitionTableFilename = "partition-table.bin";
        public readonly string updateCommand = "dotnet tool update WildernessLabs.Meadow.CLI --global";

        readonly string dfuUtilExe = "dfu-util.exe";
        readonly string libusb = "libusb-1.0.dll";
        
        public async Task DownloadLatest()
        {
            HttpClient httpClient = new HttpClient();
            var payload = await httpClient.GetStringAsync(_versionCheckUrl);
            var version = ExtractJsonValue(payload, "version");
            var minCLIVersion = ExtractJsonValue(payload, "minCLIVersion");
            var appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            
            if (minCLIVersion.ToVersion() > appVersion.ToVersion())
            {
               Console.WriteLine($"Installing OS version {version} requires the latest CLI. To update, run: {updateCommand}");
               return;
            }

            if (Directory.Exists(FirmwareDownloadsFilePath))
            {
                Directory.Delete(FirmwareDownloadsFilePath, true);
            }
            Directory.CreateDirectory(FirmwareDownloadsFilePath);

            await DownloadFile(new Uri(ExtractJsonValue(payload, "downloadUrl")));
            await DownloadFile(new Uri(ExtractJsonValue(payload, "networkDownloadUrl")));

            Console.WriteLine($"Download and extracted OS version {version} to:\r\n{FirmwareDownloadsFilePath}");
        }

        public async Task<bool> DownloadDfuUtil(bool is64bit = true)
        {
            try
            {
                if (Directory.Exists(WildernessLabsTemp))
                {
                    Directory.Delete(WildernessLabsTemp, true);
                }
                Directory.CreateDirectory(WildernessLabsTemp);

                WebClient client = new WebClient();
                if (is64bit)
                {
                    var dfuzip = "dfu-util-0.9-win64.zip";
                    var downloadUri = $"http://dfu-util.sourceforge.net/releases/{dfuzip}";

                    client.DownloadFile(downloadUri, Path.Combine(WildernessLabsTemp, dfuzip));
                    Console.WriteLine($"Downloaded {downloadUri}");

                    ZipFile.ExtractToDirectory(Path.Combine(WildernessLabsTemp, dfuzip), WildernessLabsTemp);
                    File.Copy(Path.Combine(WildernessLabsTemp, dfuzip.Substring(0, dfuzip.LastIndexOf(".")), dfuUtilExe), $@"C:\Windows\system\{dfuUtilExe}");
                    File.Copy(Path.Combine(WildernessLabsTemp, dfuzip.Substring(0, dfuzip.LastIndexOf(".")), libusb), $@"C:\Windows\system\{libusb}");
                    Console.WriteLine($"Extracted and moved {dfuUtilExe} to system folder");
                }
                else
                {
                    var dfuexe = "http://dfu-util.sourceforge.net/releases/dfu-util-0.8-binaries/win32-mingw32/dfu-util.exe";
                    var libusbdll = "http://dfu-util.sourceforge.net/releases/dfu-util-0.8-binaries/win32-mingw32/libusb-1.0.dll";
                    client.DownloadFile(dfuexe, Path.Combine(WildernessLabsTemp, dfuUtilExe));
                    client.DownloadFile(libusbdll, Path.Combine(WildernessLabsTemp, libusb));
                    File.Copy(Path.Combine(WildernessLabsTemp, dfuUtilExe), $@"C:\Windows\system32\{dfuUtilExe}");
                    File.Copy(Path.Combine(WildernessLabsTemp, libusb), $@"C:\Windows\system32\{libusb}");
                }

                return true;
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
                return false;
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
                    return (latest.ToVersion() > appVersion.ToVersion(), result.versions.Last());
                }
            }
            catch(Exception ex)
            {
            }
            return (false, string.Empty);
        }

        string ExtractJsonValue(string json, string field, string def = "")
        {
            var jo = JObject.Parse(json);
            if (jo.ContainsKey(field))
            {
                return jo[field].Value<string>();
            }
            return def;
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
