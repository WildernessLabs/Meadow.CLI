﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Meadow.Software;

public class F7FirmwarePackageCollection : IFirmwarePackageCollection
{
    /// <inheritdoc/>
    public event EventHandler<long> DownloadProgress = default!;
    public event EventHandler<FirmwarePackage?> DefaultVersionChanged = default!;

    public static string DefaultF7FirmwareStoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WildernessLabs",
        "Firmware");

    private readonly List<FirmwarePackage> _f7Packages = new();
    private FirmwarePackage? _defaultPackage;
    private readonly F7FirmwareDownloadManager _downloadManager;

    public string PackageFileRoot { get; }

    public FirmwarePackage? this[string version] => _f7Packages.FirstOrDefault(p => p.Version == version);
    public FirmwarePackage this[int index] => _f7Packages[index];

    internal F7FirmwarePackageCollection(IMeadowCloudClient meadowCloudClient)
        : this(DefaultF7FirmwareStoreRoot, meadowCloudClient)
    {
    }

    public F7FirmwarePackageCollection(string rootPath, IMeadowCloudClient meadowCloudClient)
    {
        _downloadManager = new F7FirmwareDownloadManager(meadowCloudClient);

        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
        }

        PackageFileRoot = rootPath;
    }

    public FirmwarePackage? DefaultPackage
    {
        get => _defaultPackage;
        private set
        {
            _defaultPackage = value;
            DefaultVersionChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Checks the remote (i.e. cloud) store to see if a new firmware package is available.
    /// </summary>
    /// <returns>A version number if an update is available, otherwise null</returns>
    public async Task<string?> UpdateAvailable()
    {
        var latestVersion = await _downloadManager.GetLatestAvailableVersion();

        var existing = _f7Packages.FirstOrDefault(p => p.Version == latestVersion);

        if (existing == null)
        {
            return latestVersion;
        }

        return null;
    }

    public async Task DeletePackage(string version)
    {
        var packageToDelete = _f7Packages.FirstOrDefault(p => p.Version == version);

        if (packageToDelete == null)
        {
            throw new ArgumentException($"Firmware '{version}' not found locally");
        }

        _f7Packages.Remove(packageToDelete);

        Directory.Delete(Path.Combine(PackageFileRoot, version), true);

        //are we deleting the default package
        if (DefaultPackage != null && DefaultPackage.Version == version)
        {
            FirmwarePackage? newDefault = null;

            foreach (var package in _f7Packages.OrderByDescending(p => new Version(p.Version)))
            {
                if (DefaultPackage?.Version != package.Version)
                {
                    newDefault = package;
                    break;
                }
            }

            if (newDefault != null)
            {
                await SetDefaultPackage(newDefault.Version);
            }
            else
            {
                ClearDefaultPackage();
            }
        }
    }

    public async Task SetDefaultPackage(string version)
    {
        await Refresh();

        var existing = _f7Packages.FirstOrDefault(p => p.Version == version);

        _defaultPackage = existing;

        if (existing == null)
        {
            throw new ArgumentException($"Version '{version}' not found locally");
        }

        _downloadManager.SetDefaultVersion(PackageFileRoot, version);
    }

    public void ClearDefaultPackage()
    {
        _defaultPackage = null;
    }

    public async Task<bool> IsVersionAvailableForDownload(string version)
    {
        var meta = await _downloadManager.GetReleaseMetadata(version);

        if (meta == null)
        {
            return false;
        }

        if (meta.Version != string.Empty)
        {
            return true;
        }

        return false;
    }

    public async Task<string?> GetLatestAvailableVersion()
    {
        var meta = await _downloadManager.GetReleaseMetadata();

        if (meta == null)
        {
            return null;
        }

        if (meta.Version == string.Empty)
        {
            return null;
        }

        return meta.Version;
    }

    public async Task<bool> RetrievePackage(string version, bool overwrite = false)
    {
        void ProgressHandler(object sender, long e)
        {
            DownloadProgress?.Invoke(this, e);
        }

        _downloadManager.DownloadProgress += ProgressHandler;
        try
        {
            var meta = await _downloadManager.GetReleaseMetadata(version);
            if (meta == null)
            {
                return false;
            }

            return await _downloadManager.DownloadRelease(PackageFileRoot, version, overwrite);
        }
        finally
        {
            _downloadManager.DownloadProgress -= ProgressHandler;
        }
    }

    public Task Refresh()
    {
        _f7Packages.Clear();

        var directories = Directory
            .GetDirectories(PackageFileRoot)
            .OrderByDescending(d => d);

        foreach (var directory in directories)
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

    public static class F7FirmwareFiles
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
