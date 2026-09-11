namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetReleaseMetadataTests
{
    private const string Source = "https://example.org/firmware/";

    private static string Metadata(string version) =>
        $$"""
        {"version":"{{version}}","minCLIVersion":"{{version}}","downloadUrl":"https://example.org/firmware/Meadow.OS_{{version}}.zip","networkDownloadUrl":"https://example.org/firmware/Meadow.Network_{{version}}.zip"}
        """;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReleaseMetadata_WithNullOrWhiteSpaceVersion_ShouldReturnLatestVersion(string? version)
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(Source + "latest.json", HttpStatusCode.OK, Metadata("1.8.0.0"));
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.8.0.0", result.Version);
        Assert.Equal("1.8.0.0", result.MinCLIVersion);
        Assert.Equal("https://example.org/firmware/Meadow.OS_1.8.0.0.zip", result.DownloadURL);
        Assert.Equal("https://example.org/firmware/Meadow.Network_1.8.0.0.zip", result.NetworkDownloadURL);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReleaseMetadata_WithNullOrWhiteSpaceVersion_AndNoLatestVersion_ShouldReturnNull(string? version)
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithSpecificVersion_ShouldReturnVersion()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(Source + "1.7.0.0.json", HttpStatusCode.OK, Metadata("1.7.0.0"));
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.7.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.7.0.0", result.Version);
        Assert.Equal(new Uri(Source + "1.7.0.0.json"), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GetReleaseMetadata_WithUnknownVersion_ShouldReturnNull()
    {
        // Arrange
        var handler = new StubHttpMessageHandler();
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.7.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithForbiddenResponse_ShouldReturnNull()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(Source + "1.7.0.0.json", HttpStatusCode.Forbidden);
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

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
            .Map(Source + "1.8.0.0.json", HttpStatusCode.InternalServerError);
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => downloadManager.GetReleaseMetadata("1.8.0.0"));

        // Assert
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithEmptyVersionInResponse_ShouldReturnNull()
    {
        // Arrange
        var handler = new StubHttpMessageHandler()
            .Map(Source + "latest.json", HttpStatusCode.OK, """{"version":""}""");
        var downloadManager = new F7FirmwareDownloadManager(handler.CreateClient(), Source);

        // Act
        var result = await downloadManager.GetReleaseMetadata();

        // Assert
        Assert.Null(result);
    }
}
