using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.Cloud.Client.Unit.Tests;

public class MeadowCloudClientBaseTests
{
    public class MeadowCloudClientBaseUnderTest : MeadowCloudClientBase
    {
        public new Task<MeadowCloudResponse<TResult>> ProcessResponseAsync<TResult>(HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            return base.ProcessResponseAsync<TResult>(response, cancellationToken);
        }
    }

    public class TestResult { public string PropertyOne { get; set; } = string.Empty; }

    private readonly MeadowCloudClientBaseUnderTest _clientBase = new();

    [Fact]
    public async Task ProcessResponseAsync_WithNullResponse_ShouldThrowException()
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _clientBase.ProcessResponseAsync<object>(null!));
    }

    [Fact]
    public async Task ProcessResponseAsync_WithSuccessStatusCode_AndNullContent_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = null
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponseAsync<object>(response));

        // Assert
        Assert.Equal(@"Response was null which was not expected.

Status: OK
Response: 
(null)", ex.Message);
    }

    [Fact]
    public async Task ProcessResponseAsync_WithSuccessStatusCode_AndReadFromJsonFailure_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("This is a string.")
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponseAsync<object>(response));

        // Assert
        Assert.Equal(@"Content-Type of response is 'text/plain' which is not supported for deserialization of the response body stream as System.Object. Content-Type must be 'application/json.'

Status: OK
Response: 
This is a string.", ex.Message);
    }

    [Fact]
    public async Task ProcessResponseAsync_WithSuccessStatusCode_AndNullResult_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(null, typeof(object))
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponseAsync<object>(response));

        // Assert
        Assert.Equal(@"Response was null which was not expected.

Status: OK
Response: 
(null)", ex.Message);
    }

    [Fact]
    public async Task ProcessResponseAsync_WithSuccessStatusCode_AndValidResult_ShouldReturnResult()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TestResult
            {
                PropertyOne = "TestValue"
            })
        };

        // Act
        var result = await _clientBase.ProcessResponseAsync<TestResult>(response);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestValue", result.Result.PropertyOne);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "The request is missing required information or is malformed.")]
    [InlineData(HttpStatusCode.Unauthorized, "The request failed due to invalid credentials.")]
    [InlineData(HttpStatusCode.NotFound, "The HTTP status code of the response was not expected (404).")]
    [InlineData(HttpStatusCode.InternalServerError, "The HTTP status code of the response was not expected (500).")]
    public async Task ProcessResponseAsync_WithUnsuccessfulStatusCode_ShouldThrowExceptionAndGiveMessage(HttpStatusCode httpStatusCode, string message)
    {
        // Arrange
        var response = new HttpResponseMessage(httpStatusCode)
        {
            Content = null
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponseAsync<object>(response));

        // Assert
        Assert.Equal(@$"{message}

Status: {httpStatusCode}
Response: 
", ex.Message);
    }
}
