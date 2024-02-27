using Meadow.Cloud.Client.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.Cloud.Client.Unit.Tests.UserClientTests;

public class GetUserTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly UserClient _userClient;

    public GetUserTests()
    {
        _handler = A.Fake<FakeableHttpMessageHandler>();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://example.org") };

        A.CallTo(() => _handler
           .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
           .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        var context = new MeadowCloudContext(httpClient, new Uri("https://example.org"), new MeadowCloudUserAgent("Meadow.Cloud.Client.Unit.Tests"));
        _userClient = new UserClient(context, NullLogger.Instance);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetUser_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/users/me"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _userClient.GetUser());
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetUser_WithNotFoundResponse_ShouldReturnNull()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/users/me"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var response = await _userClient.GetUser();

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task GetUser_WithResponse_ShouldReturnResult()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/users/me"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GetUserResponse("userId", "userEmail", "firstName", "lastName", "fullName"))
            });

        // Act
        var response = await _userClient.GetUser();

        // Assert
        Assert.NotNull(response);
        Assert.Equal("userId", response.Id);
    }
}

