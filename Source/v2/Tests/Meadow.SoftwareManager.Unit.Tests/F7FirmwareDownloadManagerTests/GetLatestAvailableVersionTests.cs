using Meadow.Software;
using Meadow.SoftwareManager.Unit.Tests.Builders;

namespace Meadow.SoftwareManager.Unit.Tests.F7FirmwareDownloadManagerTests;

public class GetLatestAvailableVersionTests
{
    [Fact]
    public async Task GetLatestAvailableVersion_WithLatestVersionFound_ShouldReturnVersion()
    {
        // Arrange
        var client = new MeadowCloudClientBuilder()
            .WithFirmware("Meadow_Beta", "1.8.0.0")
            .WithFirmware("Meadow_Beta", "1.7.0.0")
            .WithFirmwareReference("Meadow_Beta", "latest", "1.8.0.0")
            .Build();
        var downloadManager = new F7FirmwareDownloadManager("CLI.Test", client);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("1.8.0.0", result);
    }

    [Fact]
    public async Task GetLatestAvailableVersion_WithLatestVersionNotFound_ShouldReturnEmptyString()
    {
        // Arrange
        var client = new MeadowCloudClientBuilder()
            .WithFirmware("Meadow_Beta", "1.8.0.0")
            .WithFirmware("Meadow_Beta", "1.7.0.0")
            .Build();
        var downloadManager = new F7FirmwareDownloadManager("CLI.Test", client);

        // Act
        var result = await downloadManager.GetLatestAvailableVersion();

        // Assert
        Assert.Equal("", result);
    }
}
