using System;
using System.IO;
using System.Threading.Tasks;


namespace Meadow.Software;

public class FileManager
{
    public static readonly string WildernessTempFolderPath = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "WildernessLabs",
               "temp");
    /*
    public static readonly string OsFilename = "Meadow.OS.bin";
    public static readonly string RuntimeFilename = "Meadow.OS.Runtime.bin";
    public static readonly string NetworkBootloaderFilename = "bootloader.bin";
    public static readonly string NetworkMeadowCommsFilename = "MeadowComms.bin";
    public static readonly string NetworkPartitionTableFilename = "partition-table.bin";
    internal static readonly string VersionCheckUrlRoot =
        "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/";
    */

    public FirmwareStore Firmware { get; }

    public FileManager()
    {
        Firmware = new FirmwareStore();
        var f7Collection = new F7FirmwarePackageCollection();
        Firmware.AddCollection("Meadow F7", f7Collection);
    }

    public async Task Refresh()
    {
        foreach (var c in Firmware)
        {
            await c.Refresh();
        }
    }

    /*
    private void GetAllLocalFirmwareVersions()
    {
    }

    public bool DownloadRuntimeVersion(string version)
    {
    }

    public static string? GetLocalPathToRuntimeVersion(string version)
    {
    }

    public static string[] GetLocalRuntimeVersions()
    {
    }
    */
}