namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetLatestAvailableVersionTests
{
    private const string Source = "https://example.org/firmware/";

    [Fact]
    public async Task GetLatestAvailableVersion_WithLatestVersionFound_ShouldReturnVersion()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(Source + "latest.json", HttpStatusCode.OK, """
                {"version":"1.8.0.0","minCLIVersion":"1.8.0.0","downloadUrl":"https://example.org/firmware/Meadow.OS_1.8.0.0.zip","networkDownloadUrl":"https://example.org/firmware/Meadow.Network_1.8.0.0.zip"}
                """);
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("1.8.0.0", result);
        Assert.Equal(new Uri(Source + "latest.json"), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithLatestVersionNotFound_ShouldReturnEmptyString()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithVersionThatReturnsErrorResponse_ShouldThrowException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(Source + "latest.json", HttpStatusCode.InternalServerError);
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => downloadManager.GetLatestAvailableVersion());

        // Assert
        Assert.Contains("500", ex.Message);
        Assert.Contains(Source + "latest.json", ex.Message);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithDefaultSource_ShouldUsePublicBucket()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient());

        // Act
        await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal(new Uri(F7FirmwareDownloadManager.DefaultFirmwareSourceUrl + "latest.json"), Assert.Single(handler.Requests));
    }
}
