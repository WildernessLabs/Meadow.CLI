using System;
using System.Net;

namespace Meadow.Cloud.Client.Firmware;

public interface IFirmwareClient
{
    Task<MeadowCloudResponse<IEnumerable<GetFirmwareVersionsResponse>>> GetFirmwareVersionsAsync(string type, CancellationToken cancellationToken = default);

    Task<MeadowCloudResponse<GetFirmwareVersionResponse>> GetFirmwareVersionAsync(string type, string version, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetFirmwareDownloadResponseAsync(string path, CancellationToken cancellationToken = default);
}

public class FirmwareClient : MeadowCloudClientBase, IFirmwareClient
{
    private readonly HttpClient _httpClient;

    public FirmwareClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MeadowCloudResponse<IEnumerable<GetFirmwareVersionsResponse>>> GetFirmwareVersionsAsync(string type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
        }

        using var request = CreateHttpRequestMessage(HttpMethod.Get, "api/v1/firmware/{0}", type);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        return await ProcessResponseAsync<IEnumerable<GetFirmwareVersionsResponse>>(response, cancellationToken);
    }

    public async Task<MeadowCloudResponse<GetFirmwareVersionResponse>> GetFirmwareVersionAsync(string type, string version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException($"'{nameof(version)}' cannot be null or whitespace.", nameof(version));
        }

        if (version.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{nameof(version)}' cannot be a string that ends with '.zip'.", nameof(version));
        }

        using var request = CreateHttpRequestMessage(HttpMethod.Get, "api/v1/firmware/{0}/{1}", type, version);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        return await ProcessResponseAsync<GetFirmwareVersionResponse>(response, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetFirmwareDownloadResponseAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
        }

        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{nameof(path)}' must be a string that ends with '.zip'.", nameof(path));
        }

        var baseAddress = _httpClient.BaseAddress?.ToString();
        if (baseAddress != null && path.StartsWith(baseAddress))
        {
            path = path[baseAddress.Length..];
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var headers = GetHeaders(response);
            var statusCode = response.StatusCode;

            var message = statusCode switch
            {
                HttpStatusCode.BadRequest => "The request is missing required information or is malformed.",
                HttpStatusCode.Unauthorized => "The request failed due to invalid credentials.",
                _ => "The HTTP status code of the response was not expected (" + (int)statusCode + ")."
            };

            response.Dispose();
            throw new MeadowCloudException(message, response.StatusCode, string.Empty, headers, null);
        }

        return response;
    }
}
