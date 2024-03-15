namespace Meadow.Cloud.Client.Unit.Tests.FirmwareClientTests;

public class GetDownloadResponseTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly FirmwareClient _firmwareClient;

    public GetDownloadResponseTests()
    {
        _handler = A.Fake<FakeableHttpMessageHandler>();
        var httpClient = new HttpClient(_handler);

        A.CallTo(() => _handler
           .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
           .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        var context = new MeadowCloudContext(httpClient, new Uri("https://example.org"), new MeadowCloudUserAgent("Meadow.Cloud.Client.Unit.Tests"));
        _firmwareClient = new FirmwareClient(context, NullLogger.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Meadow.OS_1.8.0.0")]
    [InlineData("/api/v1/firmware/Meadow_Cloud/")]
    [InlineData("https://example.org/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.txt")]
    public async Task GetDownloadReponse_WithNullOrWhiteSpaceUrlOrInvalidUrl_ShouldThrowException(string url)
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _firmwareClient.GetDownloadResponse(url));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetDownloadReponse_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.zip"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _firmwareClient.GetDownloadResponse("https://example.org/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.zip"));
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetDownloadReponse_WithResponse_ShouldReturnResult()
    {
        // Arrange
        using var memoryStream = new MemoryStream();
        using var streamWriter = new StreamWriter(memoryStream) { AutoFlush = true };
        streamWriter.Write("This is a test value.");
        memoryStream.Position = 0;

        var content = new StreamContent(memoryStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.zip"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });

        // Act
        using var response = await _firmwareClient.GetDownloadResponse("https://example.org/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.zip");
        using var stream = await response.Content.ReadAsStreamAsync();

        // Assert
        using var streamReader = new StreamReader(stream);
        var actualContent = await streamReader.ReadToEndAsync();
        Assert.Equal("This is a test value.", actualContent);
    }
}
