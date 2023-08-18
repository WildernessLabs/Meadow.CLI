using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Meadow.Software;

public interface IFirmwarePackageCollection : IEnumerable<FirmwarePackage>
{
    /// <summary>
    /// Event for download progress. 
    /// </summary>
    /// <remarks>
    /// EventArgs are the total number of bytes retrieved
    /// </remarks>
    public event EventHandler<long> DownloadProgress;

    FirmwarePackage? DefaultPackage { get; }
    Task SetDefaultPackage(string version);
    Task DeletePackage(string version);
    Task Refresh();
    Task<string?> GetLatestAvailableVersion();
    Task<string?> UpdateAvailable();
    Task<bool> IsVersionAvailableForDownload(string version);
    Task<bool> RetrievePackage(string version, bool overwrite = false);
}
