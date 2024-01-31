using Meadow.Software;

namespace Meadow.SoftwareManager.Unit.Tests;

public class F7CollectionTests
{
    [Fact]
    public void TestCollectionPopulationFromLocalFolder()
    {
        var collection = new F7FirmwarePackageCollection(F7FirmwarePackageCollection.DefaultF7FirmwareStoreRoot);

        collection.Refresh();
    }
}