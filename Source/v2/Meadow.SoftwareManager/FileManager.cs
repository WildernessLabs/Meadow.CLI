using Meadow.Cloud.Client;
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

    public FirmwareStore Firmware { get; }

    public FileManager(IMeadowCloudClient meadowCloudClient)
    {
        Firmware = new FirmwareStore();
        var f7Collection = new F7FirmwarePackageCollection(meadowCloudClient);
        Firmware.AddCollection("Meadow F7", f7Collection);
    }

    public async Task Refresh()
    {
        foreach (var c in Firmware)
        {
            await c.Refresh();
        }
    }
}