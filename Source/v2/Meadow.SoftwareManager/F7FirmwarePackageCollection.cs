using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Meadow.Software;

public class F7FirmwarePackageCollection : IFirmwarePackageCollection
{
    private readonly string _rootPath;
    private List<FirmwarePackage> _f7Packages = new();

    public FirmwarePackage? DefaultPackage { get; private set; }

    public static string DefaultF7FirmwareStoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WildernessLabs",
        "Firmware");

    internal F7FirmwarePackageCollection()
        : this(DefaultF7FirmwareStoreRoot)
    {
    }

    internal F7FirmwarePackageCollection(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
        }

        _rootPath = rootPath;
    }

    /// <summary>
    /// Checks the remote (i.e. cloud) store to see if a new firmware package is available.
    /// </summary>
    /// <returns>A version number if an update is available, otherwise null</returns>
    public async Task<string?> UpdateAvailable()
    {
        var downloadManager = new F7FirmwareDownloadManager();

        var latestVersion = await downloadManager.GetLatestAvailableVersion();

        var existing = _f7Packages.FirstOrDefault(p => p.Version == latestVersion);

        if (existing == null)
        {
            return latestVersion;
        }

        return null;
    }

    public Task Refresh()
    {
        _f7Packages.Clear();

        foreach (var directory in Directory.GetDirectories(_rootPath))
        {
            var hasFiles = false;

            var package = new FirmwarePackage
            {
                Version = Path.GetFileName(directory)
            };

            foreach (var file in Directory.GetFiles(directory))
            {
                var fn = Path.GetFileName(file);
                switch (fn)
                {
                    case F7FirmwareFiles.CoprocBootloaderFile:
                        package.CoprocBootloader = fn;
                        hasFiles = true;
                        break;
                    case F7FirmwareFiles.CoprocPartitionTableFile:
                        package.CoprocPartitionTable = fn;
                        hasFiles = true;
                        break;
                    case F7FirmwareFiles.CoprocApplicationFile:
                        package.CoprocApplication = fn;
                        hasFiles = true;
                        break;
                    case F7FirmwareFiles.OSWithBootloaderFile:
                        package.OSWithBootloader = fn;
                        hasFiles = true;
                        break;
                    case F7FirmwareFiles.OsWithoutBootloaderFile:
                        package.OsWithoutBootloader = fn;
                        hasFiles = true;
                        break;
                    case F7FirmwareFiles.RuntimeFile:
                        package.Runtime = fn;
                        hasFiles = true;
                        break;
                }
            }

            if (Directory.Exists(Path.Combine(directory, F7FirmwareFiles.BclFolder)))
            {
                package.BclFolder = F7FirmwareFiles.BclFolder;
            }

            if (hasFiles)
            {
                _f7Packages.Add(package);
            }
        }

        var fi = new FileInfo(Path.Combine(_rootPath, "latest.txt"));
        if (fi.Exists)
        {
            // get default
            using var reader = fi.OpenText();
            var content = reader.ReadToEnd().Trim();

            // does it actually exist?
            DefaultPackage = _f7Packages.FirstOrDefault(p => p.Version == content);
        }

        return Task.CompletedTask;
    }

    public IEnumerator<FirmwarePackage> GetEnumerator()
    {
        return _f7Packages.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal static class F7FirmwareFiles
    {
        public const string CoprocBootloaderFile = "bootloader.bin";
        public const string CoprocPartitionTableFile = "partition-table.bin";
        public const string CoprocApplicationFile = "MeadowComms.bin";
        public const string OSWithBootloaderFile = "Meadow.OS.bin";
        public const string OsWithoutBootloaderFile = "Meadow.OS.Update.bin";
        public const string RuntimeFile = "Meadow.OS.Runtime.bin";
        public const string BclFolder = "meadow_assemblies";
    }
}
