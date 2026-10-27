namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetReleaseMetadataTests
{
    private const string Repository = "example/releases";
    private const string ReleasesUrl = "https://api.github.com/repos/example/releases/releases?per_page=100";

    private static string ManifestUrl(string tag) => ReleaseFixtures.FileUrl(Repository, tag, "manifest.json");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReleaseMetadata_WithNullOrWhiteSpaceVersion_ShouldReturnLatestVersion(string? version)
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ReleasesUrl, HttpStatusCode.OK, """[{"tag_name":"1.8.0.0","draft":false,"prerelease":false}]""")
            .Map(ManifestUrl("1.8.0.0"), HttpStatusCode.OK, ReleaseFixtures.Manifest(Repository, "1.8.0.0"));
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.8.0.0", result.Tag);
        Assert.Equal("1.8.0.0", result.Version);
        Assert.Equal("1.8.0.0", result.MinCLIVersion);
        Assert.Equal(ReleaseFixtures.Sha256Of("os"), result.Files["Meadow.OS.zip"].Sha256);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReleaseMetadata_WithNullOrWhiteSpaceVersion_AndNoReleases_ShouldReturnNull(string? version)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithSpecificVersion_ShouldReadManifestWithoutApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ManifestUrl("2.999.51.3"), HttpStatusCode.OK, ReleaseFixtures.Manifest(Repository, "2.999.51.3"));
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetReleaseMetadata("2.999.51.3");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2.999.51.3", result.Version);
        Assert.Equal(new Uri(ManifestUrl("2.999.51.3")), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GetReleaseMetadata_WithUnknownVersion_ShouldReturnNull()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.7.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithVersionThatReturnsErrorResponse_ShouldThrowException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ManifestUrl("1.8.0.0"), HttpStatusCode.InternalServerError);
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => downloadManager.GetReleaseMetadata("1.8.0.0"));

        // Assert
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithEmptyVersionInManifest_ShouldReturnNull()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ManifestUrl("1.8.0.0"), HttpStatusCode.OK, """{"version":""}""");
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.8.0.0");

        // Assert
        Assert.Null(result);
    }
}
