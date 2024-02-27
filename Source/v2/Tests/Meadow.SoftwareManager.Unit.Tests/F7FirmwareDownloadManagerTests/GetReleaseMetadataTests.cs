namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetReleaseMetadataTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReleaseMetadata_WithNullOrWhiteSpaceVersion_ShouldReturnLatestVersion(string? version)
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "latest", A<CancellationToken>._))
         .Returns(new GetFirmwareVersionResponse(
            version: "1.8.0.0",
            minCLIVersion: "1.8.0.0",
            downloadUrl: $"https://example.org/api/v1/firmware/Meadow_Beta/Meadow.OS_1.8.0.0.zip",
            networkDownloadUrl: $"https://example.org/api/v1/firmware/Meadow_Beta/Meadow.Network_1.8.0.0.zip"));

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.8.0.0", result.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReleaseMetadata_WithNullOrWhiteSpaceVersion_AndNoLatestVersion_ShouldReturnNull(string? version)
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "latest", A<CancellationToken>._))
         .Returns((GetFirmwareVersionResponse?)null);

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithSpecificVersion_ShouldReturnVersion()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "1.7.0.0", A<CancellationToken>._))
         .Returns(new GetFirmwareVersionResponse(
            version: "1.7.0.0",
            minCLIVersion: "1.7.0.0",
            downloadUrl: $"https://example.org/api/v1/firmware/Meadow_Beta/Meadow.OS_1.7.0.0.zip",
            networkDownloadUrl: $"https://example.org/api/v1/firmware/Meadow_Beta/Meadow.Network_1.7.0.0.zip"));

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.7.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.7.0.0", result.Version);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithUnknownVersion_ShouldReturnNull()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "1.7.0.0", A<CancellationToken>._))
         .Returns((GetFirmwareVersionResponse?)null);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.7.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithVersionThatReturnsErrorResponse_ShouldThrowException()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "1.8.0.0", A<CancellationToken>._))
         .ThrowsAsync(new MeadowCloudException("Test message.", HttpStatusCode.Unauthorized, null, new Dictionary<string, IEnumerable<string>>(), null));

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => downloadManager.GetReleaseMetadata("1.8.0.0"));

        // Assert
        Assert.Equal(@"Test message.

Status: Unauthorized
Response: 
(null)", ex.Message);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }
}
