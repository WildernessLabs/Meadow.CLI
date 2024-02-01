using FakeItEasy;
using Meadow.Cloud.Client.Firmware;
using Meadow.Cloud.Client.Unit.Tests.Builders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Meadow.Cloud.Client.Unit.Tests.FirmwareClientTests;

public class GetFirmwareVersionsAsyncTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly FirmwareClient _firmwareClient;

    public GetFirmwareVersionsAsyncTests()
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
    public async Task GetFirmwareVersionsAsync_WithNullOrWhiteSpaceType_ShouldThrowException(string type)
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _firmwareClient.GetFirmwareVersionsAsync(type));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetFirmwareVersionsAsync_WithUnsuccessfulResponse_ShouldThrowException(HttpStatusCode httpStatusCode)
    {
        // Arrange
        A.CallTo(() => _handler
            .FakeSendAsync(
                A<HttpRequestMessage>.That.Matches(r => r.RequestUri!.AbsolutePath == $"/api/v1/firmware/Meadow_Cloud"),
                A<CancellationToken>.Ignored))
            .Returns(new HttpResponseMessage(httpStatusCode));

        // Act/Assert
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _firmwareClient.GetFirmwareVersionsAsync("Meadow_Cloud"));
        Assert.Equal(httpStatusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetFirmwareVersionsAsync_WithResponse_ShouldReturnResult()
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
        var response = await _firmwareClient.GetFirmwareVersionsAsync("Meadow_Cloud");

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Result);
        Assert.Single(response.Result);
        Assert.Equal("1.8.0.0", response.Result.ElementAt(0).Version);
    }
}

public class GetFirmwareDownloadResponseAsyncTests
{
    private readonly FakeableHttpMessageHandler _handler;
    private readonly FirmwareClient _firmwareClient;

    public GetFirmwareDownloadResponseAsyncTests()
    {
        _handler = A.Fake<FakeableHttpMessageHandler>();
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://example.org") };

        A.CallTo(() => _handler
           .FakeSendAsync(A<HttpRequestMessage>.Ignored, A<CancellationToken>.Ignored))
           .Returns(new HttpResponseMessage(HttpStatusCode.NotFound));

        _firmwareClient = new FirmwareClient(httpClient);

    }
    [Fact]
    public async Task Playground()
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
        using var response = await _firmwareClient.GetFirmwareDownloadResponseAsync("https://example.org/api/v1/firmware/Meadow_Cloud/Meadow.OS_1.8.0.0.zip");
        using var stream = await response.Content.ReadAsStreamAsync();
        
        // Assert
        using var streamReader = new StreamReader(stream);
        var actualContent = await streamReader.ReadToEndAsync();
        Assert.Equal("This is a test value.", actualContent);
    }
}
