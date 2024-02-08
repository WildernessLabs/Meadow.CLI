using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Meadow.Cloud.Client.Unit.Tests;

public class MeadowCloudClientBaseTests
{
    public class MeadowCloudClientBaseUnderTest : MeadowCloudClientBase
    {
        public new Task EnsureSuccessfulStatusCode(HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            return base.EnsureSuccessfulStatusCode(response, cancellationToken);
        }

        public new Task<TResult> ProcessResponse<TResult>(HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            return base.ProcessResponse<TResult>(response, cancellationToken);
        }
    }

    public class TestResult { public string PropertyOne { get; set; } = string.Empty; }

    private readonly MeadowCloudClientBaseUnderTest _clientBase = new();

    [Fact]
    public async Task EnsureSuccessfulStatusCode_WithNullResponse_ShouldThrowException()
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _clientBase.EnsureSuccessfulStatusCode(null!));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task EnsureSuccessfulStatusCode_WithSuccessStatusCode_ShouldReturn(HttpStatusCode httpStatusCode)
    {
        // Arrange
        var response = new HttpResponseMessage(httpStatusCode);

        // Act
        await _clientBase.EnsureSuccessfulStatusCode(response);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "The request is missing required information or is malformed.")]
    [InlineData(HttpStatusCode.Unauthorized, "The request failed due to invalid credentials.")]
    [InlineData(HttpStatusCode.NotFound, "The HTTP status code of the response was not expected (404).")]
    [InlineData(HttpStatusCode.InternalServerError, "The HTTP status code of the response was not expected (500).")]
    public async Task EnsureSuccessfulStatusCode_WithUnsuccessfulStatusCode_ShouldThrowExceptionAndGiveMessage(HttpStatusCode httpStatusCode, string message)
    {
        // Arrange
        var response = new HttpResponseMessage(httpStatusCode) { Content = null };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.EnsureSuccessfulStatusCode(response));

        // Assert
        Assert.NotNull(ex.Response);
        Assert.Empty(ex.Response);
        Assert.Empty(ex.Headers);
        Assert.Null(ex.InnerException);

        Assert.Equal(@$"{message}

Status: {httpStatusCode}
Response: 
", ex.Message);
    }

    [Fact]
    public async Task EnsureSuccessfulStatusCode_WithUnsuccessfulStatusCodeAndContent_ShouldThrowExceptionAndHaveContent()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("This is a string.")
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.EnsureSuccessfulStatusCode(response));

        // Assert
        Assert.Equal("This is a string.", ex.Response);
    }

    [Fact]
    public async Task EnsureSuccessfulStatusCode_WithUnsuccessfulStatusCodeAndHeaders_ShouldThrowExceptionAndHaveHeaders()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        response.Headers.TryAddWithoutValidation("X-Test-Header", "TestValue");

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.EnsureSuccessfulStatusCode(response));

        // Assert
        Assert.Equal(response.Headers.ToDictionary(x => x.Key, x => x.Value), ex.Headers);
    }

    [Fact]
    public async Task ProcessResponse_WithNullResponse_ShouldThrowException()
    {
        // Act/Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _clientBase.ProcessResponse<object>(null!));
    }

    [Fact]
    public async Task ProcessResponse_WithSuccessStatusCode_AndNullContent_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = null
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponse<object>(response));

        // Assert
        Assert.Equal(@"Response was null which was not expected.

Status: OK
Response: 
(null)", ex.Message);
    }

    [Fact]
    public async Task ProcessResponse_WithSuccessStatusCode_AndNonJsonMediaType_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("This is a string.")
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponse<object>(response));

        // Assert
        Assert.Equal(@"Content-Type of response is 'text/plain' which is not supported for deserialization of the response body stream as System.Object. Content-Type must be 'application/json.'

Status: OK
Response: 
This is a string.", ex.Message);
    }

    [Fact]
    public async Task ProcessResponse_WithSuccessStatusCode_AndJsonDeserializationError_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("This is a string.", Encoding.UTF8, "application/json")
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponse<object>(response));

        // Assert
        Assert.Equal(@"Could not deserialize the response body stream as System.Object.

Status: OK
Response: 
This is a string.", ex.Message);
    }

    [Fact]
    public async Task ProcessResponse_WithSuccessStatusCode_AndNullResult_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(null, typeof(object))
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponse<object>(response));

        // Assert
        Assert.Equal(@"Response was null which was not expected.

Status: OK
Response: 
(null)", ex.Message);
    }

    [Fact]
    public async Task ProcessResponse_WithSuccessStatusCode_AndValidResult_ShouldReturnResult()
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
        var result = await _clientBase.ProcessResponse<TestResult>(response);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestValue", result.PropertyOne);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "The request is missing required information or is malformed.")]
    [InlineData(HttpStatusCode.Unauthorized, "The request failed due to invalid credentials.")]
    [InlineData(HttpStatusCode.NotFound, "The HTTP status code of the response was not expected (404).")]
    [InlineData(HttpStatusCode.InternalServerError, "The HTTP status code of the response was not expected (500).")]
    public async Task ProcessResponse_WithUnsuccessfulStatusCode_ShouldThrowExceptionAndGiveMessage(HttpStatusCode httpStatusCode, string message)
    {
        // Arrange
        var response = new HttpResponseMessage(httpStatusCode)
        {
            Content = null
        };

        // Act
        var ex = await Assert.ThrowsAsync<MeadowCloudException>(() => _clientBase.ProcessResponse<object>(response));

        // Assert
        Assert.Equal(@$"{message}

Status: {httpStatusCode}
Response: 
", ex.Message);
    }
}
