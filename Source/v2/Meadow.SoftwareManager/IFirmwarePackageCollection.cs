using System.Collections.Generic;
using System.Threading.Tasks;


namespace Meadow.Software;

public interface IFirmwarePackageCollection : IEnumerable<FirmwarePackage>
{
    FirmwarePackage? DefaultPackage { get; }
    Task Refresh();
    Task<string?> UpdateAvailable();
}
