using System;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Meadow.Cloud.Client;

public abstract class MeadowCloudClientBase
{
    protected HttpRequestMessage CreateHttpRequestMessage(HttpMethod httpMethod, string requestUriFormat, params string[] args)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.AppendFormat(requestUriFormat, args.Select(arg => Uri.EscapeDataString(arg)).ToArray());

        return new HttpRequestMessage(httpMethod, new Uri(urlBuilder.ToString(), UriKind.Relative));
    }

    protected IReadOnlyDictionary<string, IEnumerable<string>> GetHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, IEnumerable<string>>();
        foreach (var (key, value) in response.Headers)
        {
            headers[key] = value;
        }

        if (response.Content != null && response.Content.Headers != null)
        {
            foreach (var (key, value) in response.Content.Headers)
            {
                headers[key] = value;
            }
        }
        return headers;
    }

    protected async Task<MeadowCloudResponse<TResult>> ProcessResponseAsync<TResult>(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        var headers = GetHeaders(response);

        if (response.IsSuccessStatusCode)
        {
            if (response.Content.Headers.ContentType?.MediaType == null)
            {
                throw new MeadowCloudException("Response was null which was not expected.", response.StatusCode, null, headers, null);
            }

            if (response.Content.Headers.ContentType.MediaType != "application/json")
            {
                var message = $"Content-Type of response is '{response.Content.Headers.ContentType.MediaType}' which is not supported for deserialization of the response body stream as {typeof(TResult).FullName}. Content-Type must be 'application/json.'";
                var content = await response.Content.ReadAsStringAsync();
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
                throw new MeadowCloudException(message, response.StatusCode, content, headers, exception);
            }

            if (result == null)
            {
                throw new MeadowCloudException("Response was null which was not expected.", response.StatusCode, null, headers, null);
            }

            return new MeadowCloudResponse<TResult>(response.StatusCode, headers, result);
        }
        else
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => "The request is missing required information or is malformed.",
                HttpStatusCode.Unauthorized => "The request failed due to invalid credentials.",
                _ => "The HTTP status code of the response was not expected (" + (int)response.StatusCode + ")."
            };

            var content = response.Content == null ? null : await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(message, response.StatusCode, content, headers, null);
        }
    }
}
