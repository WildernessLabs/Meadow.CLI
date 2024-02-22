namespace Meadow.Cloud.Client;

public abstract class MeadowCloudClientBase
{
    protected HttpClient HttpClient => MeadowCloudContext.HttpClient;
    protected MeadowCloudContext MeadowCloudContext { get; }
    protected ILogger Logger { get; }

    public MeadowCloudClientBase(MeadowCloudContext meadowCloudContext, ILogger logger)
    {
        MeadowCloudContext = meadowCloudContext;
        Logger = logger;
    }

    private void SetHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", MeadowCloudContext.UserAgent);

        if (MeadowCloudContext.Authorization != null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", MeadowCloudContext.Authorization.ToString());
        }
    }

    protected HttpRequestMessage CreateHttpRequestMessage()
    {
        var request = new HttpRequestMessage();
        SetHeaders(request);

        return request;
    }

    protected HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, new Uri(MeadowCloudContext.BaseAddress, requestUri));
        SetHeaders(request);

        return request;
    }

    protected HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, Uri requestUri)
    {
        var request = new HttpRequestMessage(method, new Uri(MeadowCloudContext.BaseAddress, requestUri));
        SetHeaders(request);

        return request;
    }

    protected HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string requestUriFormat, params string[] args)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.AppendFormat(requestUriFormat, args.Select(arg => Uri.EscapeDataString(arg)).ToArray());

        return CreateHttpRequestMessage(method, new Uri(urlBuilder.ToString(), UriKind.Relative));
    }

    protected HttpRequestMessage CreateHttpRequestMessage<T>(HttpMethod method, string requestUri, T request)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(request);
        var content = new ByteArrayContent(json);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var httpRequest = new HttpRequestMessage(method, new Uri(MeadowCloudContext.BaseAddress, requestUri))
        {
            Content = content
        };
        SetHeaders(httpRequest);

        return httpRequest;
    }

    private static IReadOnlyDictionary<string, IEnumerable<string>> GetHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IEnumerable<string>>();
        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value;
        }

        if (response.Content != null && response.Content.Headers != null)
        {
            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = header.Value;
            }
        }
        return headers;
    }

    protected static async Task EnsureSuccessfulStatusCode(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var headers = GetHeaders(response);
        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "The request is missing required information or is malformed.",
            HttpStatusCode.Unauthorized => "The request failed due to invalid credentials.",
            _ => "The HTTP status code of the response was not expected (" + (int)response.StatusCode + ")."
        };

        var content = response.Content == null ? null : await response.Content.ReadAsStringAsync();
        throw new MeadowCloudException(message, response.StatusCode, content, headers, null);
    }

    protected static async Task<TResult> ProcessResponse<TResult>(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        await EnsureSuccessfulStatusCode(response);

        if (response.Content.Headers.ContentType?.MediaType == null)
        {
            var headers = GetHeaders(response);
            throw new MeadowCloudException("Response was null which was not expected.", response.StatusCode, null, headers, null);
        }

        if (response.Content.Headers.ContentType.MediaType != "application/json")
        {
            var message = $"Content-Type of response is '{response.Content.Headers.ContentType.MediaType}' which is not supported for deserialization of the response body stream as {typeof(TResult).FullName}. Content-Type must be 'application/json.'";
            var content = await response.Content.ReadAsStringAsync();
            var headers = GetHeaders(response);
            throw new MeadowCloudException(message, response.StatusCode, content, headers, null);
        }

        TResult? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<TResult>((JsonSerializerOptions?)null, cancellationToken);
        }
        catch (JsonException exception)
        {
            var message = "Could not deserialize the response body stream as " + typeof(TResult).FullName + ".";
            var content = await response.Content.ReadAsStringAsync();
            var headers = GetHeaders(response);
            throw new MeadowCloudException(message, response.StatusCode, content, headers, exception);
        }

        if (result == null)
        {
            var headers = GetHeaders(response);
            throw new MeadowCloudException("Response was null which was not expected.", response.StatusCode, null, headers, null);
        }

        return result;
    }
}
