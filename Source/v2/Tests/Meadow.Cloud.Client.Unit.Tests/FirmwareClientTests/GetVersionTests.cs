namespace Meadow.Cloud.Client.Unit.Tests.FirmwareClientTests;

public class GetVersionTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly FirmwareClient _firmwareClient;

    public GetVersionTests()
    {
        _handler = A.Fake<FakeableHttpMessageHandler>();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://example.org") };

        A.CallTo(() => _handler
           .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
           .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        _firmwareClient = new FirmwareClient(httpClient);
    }

    [Theory]
    [InlineData(null, "verison")]
    [InlineData("", "verison")]
    [InlineData(" ", "verison")]
    [InlineData("type", null)]
    [InlineData("type", "")]
    [InlineData("type", " ")]
    [InlineData("type", "version.zip")]
    public async Task GetVersion_WithNullOrWhiteSpaceTypeOrVersion_OrWithInvalidVersion_ShouldThrowException(string type, string version)
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _firmwareClient.GetVersion(type, version));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetVersion_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud/1.8.0.0"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _firmwareClient.GetVersion("Meadow_Cloud", "1.8.0.0"));
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetVersion_WithNotFoundResponse_ShouldReturnNull()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud/1.8.0.0"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var response = await _firmwareClient.GetVersion("Meadow_Cloud", "1.8.0.0");

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task GetVersion_WithResponse_ShouldReturnResult()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud/1.8.0.0"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GetFirmwareVersionResponse(
                    version: "1.8.0.0",
                    minCLIVersion: "1.8.0.0",
                    downloadUrl: "https://example.org/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.zip",
                    networkDownloadUrl: "https://example.org/api/v1/firmware/Meadow_Cloud/Meadow.Network_1.8.0.0.zip"))

            });

        // Act
        var response = await _firmwareClient.GetVersion("Meadow_Cloud", "1.8.0.0");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("1.8.0.0", response.Version);
    }
}