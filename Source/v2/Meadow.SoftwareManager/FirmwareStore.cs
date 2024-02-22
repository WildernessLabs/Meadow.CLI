namespace Meadow.Software;

public class FirmwareStore : IEnumerable<IFirmwarePackageCollection>
{
    private readonly Dictionary<string, IFirmwarePackageCollection> _collections = new();

    public string[] CollectionNames => _collections.Keys.ToArray();

    internal FirmwareStore()
    {
    }

    public IFirmwarePackageCollection this[string collectionName]
    {
        get => _collections[collectionName];
    }

    internal void AddCollection(string name, IFirmwarePackageCollection collection)
    {
        _collections.Add(name, collection);
    }

    public IEnumerator<IFirmwarePackageCollection> GetEnumerator()
    {
        return _collections.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
