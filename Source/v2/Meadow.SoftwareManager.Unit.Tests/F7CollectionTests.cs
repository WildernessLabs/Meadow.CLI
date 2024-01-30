using Meadow.Software;

namespace Meadow.SoftwareManager.Unit.Tests;

public class F7CollectionTests
{
    [Fact]
    public void TestCollectionPopulation()
    {
        var collection = new F7FirmwarePackageCollection(new HttpClient(), F7FirmwarePackageCollection.DefaultF7FirmwareStoreRoot);

        collection.Refresh();
    }
}