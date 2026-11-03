namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetLatestAvailableVersionTests
{
    private const string Repository = "example/releases";
    private const string ReleasesUrl = "https://api.github.com/repos/example/releases/releases?per_page=100";

    private static string Manifest(string version) => ReleaseFixtures.Manifest(Repository, version);

    [Fact]
    public async Task GetLatestAvailableVersion_ShouldReturnHighestFullRelease()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ReleasesUrl, HttpStatusCode.OK, """
                [
                  {"tag_name":"1.0.2.0","draft":false,"prerelease":false},
                  {"tag_name":"2.5.0.0","draft":false,"prerelease":false},
                  {"tag_name":"2.999.51.3","draft":false,"prerelease":true},
                  {"tag_name":"3.0.0.0","draft":true,"prerelease":false},
                  {"tag_name":"not-a-version","draft":false,"prerelease":false}
                ]
                """)
            .Map(ReleaseFixtures.FileUrl(Repository, "2.5.0.0", "manifest.json"), HttpStatusCode.OK, Manifest("2.5.0.0"));
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("2.5.0.0", result);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_ShouldSendUserAgent()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ReleasesUrl, HttpStatusCode.OK, "[]");
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.NotEmpty(Assert.Single(handler.RequestMessages).Headers.UserAgent);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithNoReleases_ShouldReturnEmptyString()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ReleasesUrl, HttpStatusCode.OK, "[]");
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithErrorResponse_ShouldThrowException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(ReleasesUrl, HttpStatusCode.Forbidden);
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Repository);

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => downloadManager.GetLatestAvailableVersion());

        // Assert
        Assert.Contains("403", ex.Message);
        Assert.Contains(ReleasesUrl, ex.Message);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithDefaultRepository_ShouldUseMeadowReleases()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient());

        // Act
        await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal(
            new Uri("https://api.github.com/repos/WildernessLabs/Meadow.Releases/releases?per_page=100"),
            Assert.Single(handler.Requests));
    }
}
