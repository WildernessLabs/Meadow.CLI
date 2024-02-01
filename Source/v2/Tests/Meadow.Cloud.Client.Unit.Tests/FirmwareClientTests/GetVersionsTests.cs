namespace Meadow.Cloud.Client.Unit.Tests.FirmwareClientTests;

public class GetVersionsTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly FirmwareClient _firmwareClient;

    public GetVersionsTests()
    {
        _handler = A.Fake<FakeableHttpMessageHandler>();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://example.org") };

        A.CallTo(() => _handler
           .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
           .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        _firmwareClient = new FirmwareClient(httpClient);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetVersions_WithNullOrWhiteSpaceType_ShouldThrowException(string type)
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _firmwareClient.GetVersions(type));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetVersions_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _firmwareClient.GetVersions("Meadow_Cloud"));
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetVersions_WithNotFoundResponse_ShouldReturnEmptyResult()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var response = await _firmwareClient.GetVersions("Meadow_Cloud");

        // Assert
        Assert.Empty(response);
    }

    [Fact]
    public async Task GetVersions_WithResponse_ShouldReturnResult()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<GetFirmwareVersionsResponse>
                {
                    new ("1.8.0.0", DateTimeOffset.UtcNow)
                })
            });

        // Act
        var response = await _firmwareClient.GetVersions("Meadow_Cloud");

        // Assert
        Assert.NotNull(response);
        Assert.Single(response);
        Assert.Equal("1.8.0.0", response.ElementAt(0).Version);
    }
}
