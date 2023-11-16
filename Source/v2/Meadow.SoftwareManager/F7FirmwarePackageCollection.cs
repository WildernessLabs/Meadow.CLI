﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Meadow.Software;

public class F7FirmwarePackageCollection : IFirmwarePackageCollection
{
    /// <inheritdoc/>
    public event EventHandler<long>? DownloadProgress;

    public string PackageFileRoot { get; }

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

        PackageFileRoot = rootPath;
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

    public Task DeletePackage(string version)
    {
        var existing = _f7Packages.FirstOrDefault(p => p.Version == version);

        if (existing == null)
        {
            throw new ArgumentException($"Version '{version}' not found locally.");
        }

        // if we're deleting the default, we need to det another default
        var i = _f7Packages.Count - 1;
        if (DefaultPackage != null)
        {
            while (DefaultPackage.Version == _f7Packages[i].Version)
            {
                i--;
            }
            var newDefault = _f7Packages[i].Version;
            _f7Packages.Remove(DefaultPackage);

            if (!string.IsNullOrEmpty(newDefault))
                SetDefaultPackage(newDefault);
        }

        var path = Path.Combine(PackageFileRoot, version);

        Directory.Delete(path, true);

        return Task.CompletedTask;
    }

    public Task SetDefaultPackage(string version)
    {
        // Refresh the list, in case we've just downloaded it.
        Refresh();

        var existing = _f7Packages.FirstOrDefault(p => p.Version == version);

        if (existing == null)
        {
            throw new ArgumentException($"Version '{version}' not found locally.");
        }

        var downloadManager = new F7FirmwareDownloadManager();
        downloadManager.SetDefaultVersion(PackageFileRoot, version);

        return Task.CompletedTask;
    }

    public async Task<bool> IsVersionAvailableForDownload(string version)
    {
        var downloadManager = new F7FirmwareDownloadManager();

        var meta = await downloadManager.GetReleaseMetadata(version);

        if (meta == null) return false;
        if (meta.Version != string.Empty) return true;

        return false;
    }

    public async Task<string?> GetLatestAvailableVersion()
    {
        var downloadManager = new F7FirmwareDownloadManager();

        var meta = await downloadManager.GetReleaseMetadata();

        if (meta == null) return null;
        if (meta.Version == string.Empty) return null;

        return meta.Version;
    }

    public async Task<bool> RetrievePackage(string version, bool overwrite = false)
    {
        var downloadManager = new F7FirmwareDownloadManager();

        void ProgressHandler(object sender, long e)
        {
            DownloadProgress?.Invoke(this, e);
        }

        downloadManager.DownloadProgress += ProgressHandler;
        try
        {
            var meta = await downloadManager.GetReleaseMetadata(version);
            if (meta == null) return false;

            return await downloadManager.DownloadRelease(PackageFileRoot, version, overwrite);
        }
        finally
        {
            downloadManager.DownloadProgress -= ProgressHandler;
        }
    }

    public Task Refresh()
    {
        _f7Packages.Clear();

        foreach (var directory in Directory.GetDirectories(PackageFileRoot))
        {
            var hasFiles = false;

            var package = new FirmwarePackage(this)
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

        var fi = new FileInfo(Path.Combine(PackageFileRoot, "latest.txt"));
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
