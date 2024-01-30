using Meadow.Software;
using Meadow.SoftwareManager.Unit.Tests.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
        var client = new MeadowCloudClientBuilder()
            .WithFirmware("Meadow_Beta", "1.8.0.0")
            .WithFirmware("Meadow_Beta", "1.7.0.0")
            .WithFirmwareReference("Meadow_Beta", "latest", "1.8.0.0")
            .Build();
        var downloadManager = new F7FirmwareDownloadManager(client);

        // Act
        var result = await downloadManager.GetReleaseMetadata(version);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.8.0.0", result.Version);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithSpecificVersion_ShouldReturnVersion()
    {
        // Arrange
        var client = new MeadowCloudClientBuilder()
            .WithFirmware("Meadow_Beta", "1.8.0.0")
            .WithFirmware("Meadow_Beta", "1.7.0.0")
            .WithFirmwareReference("Meadow_Beta", "latest", "1.8.0.0")
            .Build();
        var downloadManager = new F7FirmwareDownloadManager(client);

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
        var client = new MeadowCloudClientBuilder()
            .WithFirmware("Meadow_Beta", "1.8.0.0")
            .WithFirmware("Meadow_Beta", "1.7.0.0")
            .WithFirmwareReference("Meadow_Beta", "latest", "1.8.0.0")
            .Build();
        var downloadManager = new F7FirmwareDownloadManager(client);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.6.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithVersionThatReturnsErrorResponse_ShouldReturnNull()
    {
        // Arrange
        var client = new MeadowCloudClientBuilder()
            .WithFirmwareResponse("Meadow_Beta", "1.8.0.0", new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .Build();
        var downloadManager = new F7FirmwareDownloadManager(client);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.8.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReleaseMetadata_WithVersionThatReturnsTextResponse_ShouldReturnNull()
    {
        // Arrange
        var client = new MeadowCloudClientBuilder()
            .WithFirmwareResponse("Meadow_Beta", "1.8.0.0", new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("This is content.")
            })
            .Build();
        var downloadManager = new F7FirmwareDownloadManager(client);

        // Act
        var result = await downloadManager.GetReleaseMetadata("1.8.0.0");

        // Assert
        Assert.Null(result);
    }
}
