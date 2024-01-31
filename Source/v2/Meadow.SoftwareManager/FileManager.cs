using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Meadow.Software;

public class FileManager
{
    public static readonly string WildernessTempFolderPath = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "WildernessLabs",
               "temp");

    public const string UserAgentCli = "Meadow.Cli";
    public const string UserAgentWorkbench = "Meadow.Workbench";

    public FirmwareStore Firmware { get; }

    public HttpClient MeadowCloudClient { get; }

    public FileManager(string userAgent, HttpClient? meadowCloudClient = null)
    {
        Firmware = new FirmwareStore();
        var f7Collection = new F7FirmwarePackageCollection(userAgent, meadowCloudClient);
        Firmware.AddCollection("Meadow F7", f7Collection);
        MeadowCloudClient = meadowCloudClient;
    }

    public async Task Refresh()
    {
        foreach (var c in Firmware)
        {
            await c.Refresh();
        }
    }
}