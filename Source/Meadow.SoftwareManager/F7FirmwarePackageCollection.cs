using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Meadow.Software;

/// <summary>
/// Represents a collection of firmware packages for the F7 series.
/// </summary>
public class F7FirmwarePackageCollection : IFirmwarePackageCollection
{
    /// <inheritdoc/>
    public event EventHandler<long> DownloadProgress = default!;

    /// <inheritdoc/>
    public event EventHandler<FirmwarePackage?> DefaultVersionChanged = default!;

    /// <summary>
    /// The default root directory for storing F7 firmware packages.
    /// </summary>
    public static string DefaultF7FirmwareStoreRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WildernessLabs",
        "Firmware");

    private readonly List<FirmwarePackage> _f7Packages = new();
    private FirmwarePackage? _defaultPackage;
    private readonly F7FirmwareDownloadManager _downloadManager;

    /// <summary>
    /// Gets the root directory for storing firmware packages.
    /// </summary>
    public string PackageFileRoot { get; }

    /// <summary>
    /// Gets the firmware package with the specified version.
    /// </summary>
    /// <param name="version">The version of the firmware package.</param>
    /// <returns>The firmware package if found; otherwise, null.</returns>
    public FirmwarePackage? this[string version] => _f7Packages.FirstOrDefault(p => p.Version == version);

    /// <summary>
    /// Gets the firmware package at the specified index.
    /// </summary>
    /// <param name="index">The index of the firmware package.</param>
    /// <returns>The firmware package at the specified index.</returns>
    public FirmwarePackage this[int index] => _f7Packages[index];

    /// <summary>
    /// Initializes a new instance of the <see cref="F7FirmwarePackageCollection"/> class.
    /// </summary>
    /// <param name="meadowCloudClient">The Meadow cloud client.</param>
    internal F7FirmwarePackageCollection(IMeadowCloudClient meadowCloudClient)
        : this(DefaultF7FirmwareStoreRoot, meadowCloudClient)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="F7FirmwarePackageCollection"/> class.
    /// </summary>
    /// <param name="rootPath">The root path for storing firmware packages.</param>
    /// <param name="meadowCloudClient">The Meadow cloud client.</param>
    public F7FirmwarePackageCollection(string rootPath, IMeadowCloudClient meadowCloudClient)
    {
        _downloadManager = new F7FirmwareDownloadManager(meadowCloudClient);

        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
        }

        PackageFileRoot = rootPath;
    }

    /// <summary>
    /// Gets the default firmware package.
    /// </summary>
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
    /// <returns>A version number if an update is available, otherwise null.</returns>
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

    /// <summary>
    /// Deletes the specified firmware package.
    /// </summary>
    /// <param name="version">The version of the firmware package to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the specified version is not found locally.</exception>
    public async Task DeletePackage(string version)
    {
        var packageToDelete = _f7Packages.FirstOrDefault(p => p.Version == version);

        if (packageToDelete == null)
        {
            throw new ArgumentException($"Firmware '{version}' not found locally");
        }

        _f7Packages.Remove(packageToDelete);

        Directory.Delete(Path.Combine(PackageFileRoot, version), true);

        // Check if we are deleting the default package
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

    /// <summary>
    /// Sets the default firmware package to the specified version.
    /// </summary>
    /// <param name="version">The version to set as the default package.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the specified version is not found locally.</exception>
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

    /// <summary>
    /// Clears the default firmware package.
    /// </summary>
    public void ClearDefaultPackage()
    {
        _defaultPackage = null;
    }

    /// <summary>
    /// Checks if the specified version is available for download.
    /// </summary>
    /// <param name="version">The version to check.</param>
    /// <returns>A task representing the asynchronous operation, with a result indicating whether the version is available for download.</returns>
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

    /// <summary>
    /// Gets the latest available version of the firmware package.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, with a result of the latest available version if available; otherwise, null.</returns>
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

    /// <summary>
    /// Retrieves the specified firmware package.
    /// </summary>
    /// <param name="version">The version of the firmware package to retrieve.</param>
    /// <param name="overwrite">Whether to overwrite the existing package if it already exists.</param>
    /// <returns>A task representing the asynchronous operation, with a result indicating whether the package was successfully retrieved.</returns>
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

    /// <summary>
    /// Refreshes the collection of local firmware packages.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Gets the local firmware package with the specified version.
    /// </summary>
    /// <param name="osVersion">The version of the firmware package.</param>
    /// <returns>The local firmware package if found; otherwise, null.</returns>
    public FirmwarePackage? GetLocalPackage(string osVersion)
    {
        return _f7Packages.FirstOrDefault(p => p.Version == osVersion);
    }

    /// <summary>
    /// Gets the closest local firmware package that matches the specified version,
    /// with the same major and minor version, and is equal to or lower than the specified version.
    /// </summary>
    /// <param name="osVersion">The version to match.</param>
    /// <returns>The closest matching local firmware package if found; otherwise, null.</returns>
    public FirmwarePackage? GetClosestLocalPackage(string osVersion)
    {
        if (string.IsNullOrEmpty(osVersion))
        {
            throw new ArgumentException("Version cannot be null or empty", nameof(osVersion));
        }

        var versionToCompare = new Version(osVersion);
        return _f7Packages
            .Where(p =>
            {
                var packageVersion = new Version(p.Version);
                return packageVersion.Major == versionToCompare.Major &&
                       packageVersion.Minor == versionToCompare.Minor &&
                       packageVersion <= versionToCompare;
            })
            .OrderByDescending(p => new Version(p.Version))
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of firmware packages.
    /// </summary>
    /// <returns>An enumerator for the collection of firmware packages.</returns>
    public IEnumerator<FirmwarePackage> GetEnumerator()
    {
        return _f7Packages.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of firmware packages.
    /// </summary>
    /// <returns>An enumerator for the collection of firmware packages.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Contains constants for firmware file names and directories.
    /// </summary>
    public static class F7FirmwareFiles
    {
        /// <summary>
        /// The coprocessor bootloader file name.
        /// </summary>
        public const string CoprocBootloaderFile = "bootloader.bin";

        /// <summary>
        /// The coprocessor partition table file name.
        /// </summary>
        public const string CoprocPartitionTableFile = "partition-table.bin";

        /// <summary>
        /// The coprocessor application file name.
        /// </summary>
        public const string CoprocApplicationFile = "MeadowComms.bin";

        /// <summary>
        /// The OS with bootloader file name.
        /// </summary>
        public const string OSWithBootloaderFile = "Meadow.OS.bin";

        /// <summary>
        /// The OS without bootloader file name.
        /// </summary>
        public const string OsWithoutBootloaderFile = "Meadow.OS.Update.bin";

        /// <summary>
        /// The runtime file name.
        /// </summary>
        public const string RuntimeFile = "Meadow.OS.Runtime.bin";

        /// <summary>
        /// The BCL (Base Class Library) folder name.
        /// </summary>
        public const string BclFolder = "meadow_assemblies";
    }
}