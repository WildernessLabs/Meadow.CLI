namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class DownloadReleaseTests : IDisposable
{
    private const string Repository = "example/releases";
    private const string Version = "2.999.51.3";

    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    private static StubHttpMessageHandler HandlerFor(byte[] osZip, byte[] networkZip, string manifest)
    {
        return new StubHttpMessageHandler()
            .Map(ReleaseFixtures.FileUrl(Repository, Version, "manifest.json"), HttpStatusCode.OK, manifest)
            .Map(ReleaseFixtures.FileUrl(Repository, Version, "Meadow.OS.zip"), HttpStatusCode.OK, osZip)
            .Map(ReleaseFixtures.FileUrl(Repository, Version, "Meadow.Network.zip"), HttpStatusCode.OK, networkZip);
    }

    [Fact]
    public async Task DownloadRelease_WithMatchingHashes_ShouldExtractBothPackages()
    {
        // Arrange
        var osZip = ReleaseFixtures.Zip("Meadow.OS.bin", "meadow_assemblies/System.Private.CoreLib.dll");
        var networkZip = ReleaseFixtures.Zip("MeadowComms.bin", "bootloader.bin", "partition-table.bin");
        var manifest = ReleaseFixtures.Manifest(Repository, Version, osZip, networkZip);
        var downloadManager = new F7FirmwareDownloadManager(HandlerFor(osZip, networkZip, manifest).CreateClient(), Repository);

        // Act
        var result = await downloadManager.DownloadRelease(_rootPath, Version);

        // Assert
        Assert.True(result);
        var local = Path.Combine(_rootPath, Version);
        Assert.True(File.Exists(Path.Combine(local, "Meadow.OS.bin")));
        Assert.True(File.Exists(Path.Combine(local, "meadow_assemblies", "System.Private.CoreLib.dll")));
        Assert.True(File.Exists(Path.Combine(local, "MeadowComms.bin")));
    }

    [Fact]
    public async Task DownloadRelease_WithHashMismatch_ShouldThrowAndRemoveFolder()
    {
        // Arrange
        var osZip = ReleaseFixtures.Zip("Meadow.OS.bin");
        var networkZip = ReleaseFixtures.Zip("MeadowComms.bin");
        var manifest = ReleaseFixtures.Manifest(Repository, Version, ReleaseFixtures.Zip("other.bin"), networkZip);
        var downloadManager = new F7FirmwareDownloadManager(HandlerFor(osZip, networkZip, manifest).CreateClient(), Repository);

        // Act
        var ex = await Assert.ThrowsAsync<Exception>(() => downloadManager.DownloadRelease(_rootPath, Version));

        // Assert
        Assert.Contains("SHA-256 mismatch", ex.Message);
        Assert.False(Directory.Exists(Path.Combine(_rootPath, Version)));
    }

    [Fact]
    public async Task DownloadRelease_WithPackageMissingFromManifest_ShouldThrow()
    {
        // Arrange
        var osZip = ReleaseFixtures.Zip("Meadow.OS.bin");
        var networkZip = ReleaseFixtures.Zip("MeadowComms.bin");
        var manifest = $$"""{"tag":"{{Version}}","version":"{{Version}}","files":{} }""";
        var downloadManager = new F7FirmwareDownloadManager(HandlerFor(osZip, networkZip, manifest).CreateClient(), Repository);

        // Act
        var ex = await Assert.ThrowsAsync<Exception>(() => downloadManager.DownloadRelease(_rootPath, Version));

        // Assert
        Assert.Contains("no SHA-256 for Meadow.OS.zip", ex.Message);
    }
}
