namespace Meadow.Cloud.Client.Unit.Tests.DeviceClientTests;

public class AddDeviceTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly DeviceClient _deviceClient;

    public AddDeviceTests()
    {
        _handler = A.Fake<FakeableHttpMessageHandler>();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://example.org") };

        A.CallTo(() => _handler
           .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
           .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        var context = new MeadowCloudContext(httpClient, new Uri("https://example.org"), new MeadowCloudUserAgent("Meadow.Cloud.Client.Unit.Tests"));
        _deviceClient = new DeviceClient(context, NullLogger.Instance);
    }

    [Fact]
    public async Task AddDevice_WithNullRequest_ShouldThrowException()
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _deviceClient.AddDevice(null!));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AddDevice_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        var addDeviceRequest = new AddDeviceRequest("id", "orgId", "publicKey");

        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/devices"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _deviceClient.AddDevice(addDeviceRequest));
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task AddDevice_WithResponse_ShouldReturnResult()
    {
        // Arrange
        var addDeviceRequest = new AddDeviceRequest("device-id", "device-org-id", "device-public-key");

        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/devices"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AddDeviceResponse("device-id", "name", "device-org-id", "device-collection-id"))
            });

        // Act
        var response = await _deviceClient.AddDevice(addDeviceRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("device-id", response.Id);
    }
}
