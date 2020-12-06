using Meadow;
using MeadowCLI;
using MeadowCLI.DeviceManagement;
using MeadowCLI.Hcom;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI
{
    public class DownloadManager
    {
        readonly string versionCheckUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/latest.json";
        readonly Guid DEVICE_INTERFACE_GUID_STDFU = new Guid(0x3fe809ab, 0xfb91, 0x4cb5, 0xa6, 0x43, 0x69, 0x67, 0x0d, 0x52, 0x36, 0x6e);
        public readonly string FirmwareDownloadsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "Firmware");
        string VersionCheckFile { get { return new Uri(versionCheckUrl).Segments.Last(); } }
        public readonly string osFilename = "Meadow.OS.bin";
        public readonly string runtimeFilename = "Meadow.OS.Runtime.bin";
        public readonly string networkBootloaderFilename = "bootloader.bin";
        public readonly string networkMeadowCommsFilename = "MeadowComms.bin";
        public readonly string networkPartitionTableFilename = "partition-table.bin";
        public readonly uint osAddress = 0x08000000;

        public async Task DownloadLatest()
        {
            HttpClient httpClient = new HttpClient();
            var payload = await httpClient.GetStringAsync(versionCheckUrl);
            var version = ExtractJsonValue(payload, "version");
            var minCLIVersion = ExtractJsonValue(payload, "minCLIVersion", "0.12.0");

            FileVersionInfo myFileVersionInfo =
                FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName);

            if (!CheckCompatibility(minCLIVersion, myFileVersionInfo.ProductVersion))
            {
                Console.WriteLine($"Please update Meadow.CLI to continue. Run \"dotnet tool update Meadow.CLI --global\" to update.");
                return;
            }

            if (Directory.Exists(FirmwareDownloadsFilePath))
            {
                Directory.Delete(FirmwareDownloadsFilePath, true);
            }
            Directory.CreateDirectory(FirmwareDownloadsFilePath);

            await DownloadFile(new Uri(ExtractJsonValue(payload, "downloadUrl")));
            await DownloadFile(new Uri(ExtractJsonValue(payload, "networkDownloadUrl")));

            Console.WriteLine($"Downloaded Meadow OS version {version}");
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
            webClient.DownloadFile(versionCheckUrl, Path.Combine(FirmwareDownloadsFilePath, VersionCheckFile));
            ZipFile.ExtractToDirectory(Path.Combine(FirmwareDownloadsFilePath, fileName), FirmwareDownloadsFilePath);
        }

        private bool CheckCompatibility(string minVsixVersion, string vsixVersion)
        {
            Version vsix, minVsix;
            Version.TryParse(minVsixVersion, out minVsix);
            Version.TryParse(vsixVersion, out vsix);

            return (vsix ?? new Version(0, 0, 0)) >= (minVsix ?? new Version(0, 0, 0));
        }
    }
}
