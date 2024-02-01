using static Meadow.Software.F7FirmwarePackageCollection;

namespace Meadow.SoftwareManager.Unit.Tests;

public class F7FirmwarePackageCollectionTests : IDisposable
{
    private readonly string _rootPath;

    public F7FirmwarePackageCollectionTests()
    {
        _rootPath = Directory.CreateTempSubdirectory().FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    [Fact]
    public async Task Refresh_WithNoFiles_ShouldBeEmpty()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var collection = new F7FirmwarePackageCollection(_rootPath, client);

        // Act
        await collection.Refresh();

        // Assert
        Assert.Empty(collection);
    }

    [Fact]
    public async Task Refresh_WithASinglePackage_ShouldHaveOnePackage()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var collection = new F7FirmwarePackageCollection(_rootPath, client);

        var versionPath = Path.Combine(_rootPath, "1.8.0.0");
        Directory.CreateDirectory(versionPath);
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.CoprocBootloaderFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.CoprocPartitionTableFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.CoprocApplicationFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.OSWithBootloaderFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.OsWithoutBootloaderFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.RuntimeFile), "");

        Directory.CreateDirectory(Path.Combine(versionPath, F7FirmwareFiles.BclFolder));

        // Act
        await collection.Refresh();

        // Assert
        var package = Assert.Single(collection);
        Assert.Equal("1.8.0.0", package.Version);
        Assert.Null(collection.DefaultPackage);
    }

    [Fact]
    public async Task Refresh_WithASinglePackageAndLatestFile_ShouldSetDefaultPackage()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var collection = new F7FirmwarePackageCollection(_rootPath, client);

        var versionPath = Path.Combine(_rootPath, "1.8.0.0");
        Directory.CreateDirectory(versionPath);
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.CoprocBootloaderFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.CoprocPartitionTableFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.CoprocApplicationFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.OSWithBootloaderFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.OsWithoutBootloaderFile), "");
        File.WriteAllText(Path.Combine(versionPath, F7FirmwareFiles.RuntimeFile), "");
        Directory.CreateDirectory(Path.Combine(versionPath, F7FirmwareFiles.BclFolder));

        File.WriteAllText(Path.Combine(_rootPath, "latest.txt"), "1.8.0.0");

        // Act
        await collection.Refresh();

        // Assert
        var package = Assert.Single(collection);
        Assert.Equal("1.8.0.0", package.Version);
        Assert.NotNull(collection.DefaultPackage);
        Assert.Equal("1.8.0.0", collection.DefaultPackage.Version);
    }
}
