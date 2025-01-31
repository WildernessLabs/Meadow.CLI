namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetLatestAvailableVersionTests
{
    [Fact]
    public async Task GetLatestAvailableVersion_WithLatestVersionFound_ShouldReturnVersion()
    {
        // Arrange
        var version = new GetFirmwareVersionResponse(
            version: "1.8.0.0",
            minCLIVersion: "1.8.0.0",
            downloadUrl: $"https://example.org/api/v1/firmware/Meadow_Beta/Meadow.OS_1.8.0.0.zip",
            networkDownloadUrl: $"https://example.org/api/v1/firmware/Meadow_Beta/Meadow.Network_1.8.0.0.zip");

        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "latest", A<CancellationToken>._))
         .Returns(version);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("1.8.0.0", result);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithLatestVersionNotFound_ShouldReturnEmptyString()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "latest", A<CancellationToken>._))
         .Returns((GetFirmwareVersionResponse?)null);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithVersionThatReturnsErrorResponse_ShouldThrowException()
    {
        // Arrange
        var client = A.Fake<IMeadowCloudClient>();
        var downloadManager = new F7FirmwareDownloadManager(client);

        A.CallTo(() => client.Firmware.GetVersion("Meadow_Beta", "latest", A<CancellationToken>._))
         .ThrowsAsync(new MeadowCloudException("Test message.", HttpStatusCode.Unauthorized, null, new Dictionary<string, IEnumerable<string>>(), null));

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => downloadManager.GetLatestAvailableVersion());

        // Assert
        Assert.Equal(@"Test message.

Status: Unauthorized
Response: 
(null)", ex.Message);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }
}
