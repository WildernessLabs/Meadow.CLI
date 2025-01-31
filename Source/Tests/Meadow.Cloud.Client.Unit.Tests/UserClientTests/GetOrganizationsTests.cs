using Meadow.Cloud.Client.Users;

namespace Meadow.Cloud.Client.Unit.Tests.UserClientTests;

public class GetOrganizationsTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly UserClient _userClient;

    public GetOrganizationsTests()
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
    public async Task GetOrganizations_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/users/me/orgs"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _userClient.GetOrganizations());
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetOrganizations_WithNotFoundResponse_ShouldReturnEmptyResult()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/users/me/orgs"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var response = await _userClient.GetOrganizations();

        // Assert
        Assert.Empty(response);
    }

    [Fact]
    public async Task GetOrganizations_WithResponse_ShouldReturnResult()
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/users/me/orgs"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<GetOrganizationResponse>
                {
                    new ("organizationId", "organizationName", "defaultCollectionId")
                })
            });

        // Act
        var response = await _userClient.GetOrganizations();

        // Assert
        Assert.NotNull(response);
        Assert.Single(response);
        Assert.Equal("organizationId", response.ElementAt(0).Id);
    }
}

