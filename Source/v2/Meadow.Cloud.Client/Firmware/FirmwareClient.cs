namespace Meadow.Cloud.Client.Firmware;

public interface IFirmwareClient
{
    Task<IEnumerable<GetFirmwareVersionsResponse>> GetVersions(string type, CancellationToken cancellationToken = default);

    Task<GetFirmwareVersionResponse?> GetVersion(string type, string version, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetDownloadResponse(string url, CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> GetDownloadResponse(Uri url, CancellationToken cancellationToken = default);
}

public class FirmwareClient : MeadowCloudClientBase, IFirmwareClient
{
    private readonly HttpClient _httpClient;

    public FirmwareClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<GetFirmwareVersionsResponse>> GetVersions(string type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
        }

        using var request = CreateHttpRequestMessage(HttpMethod.Get, "api/v1/firmware/{0}", type);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Enumerable.Empty<GetFirmwareVersionsResponse>();
        }

        return await ProcessResponse<IEnumerable<GetFirmwareVersionsResponse>>(response, cancellationToken);
    }

    public async Task<GetFirmwareVersionResponse?> GetVersion(string type, string version, CancellationToken cancellationToken = default)
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

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ProcessResponse<GetFirmwareVersionResponse>(response, cancellationToken);
    }

    public async Task<HttpResponseMessage> GetDownloadResponse(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException($"'{nameof(url)}' cannot be null or whitespace.", nameof(url));
        }

        if (!url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{nameof(url)}' must be a URL that ends with '.zip'.", nameof(url));
        }

        var baseAddress = _httpClient.BaseAddress?.ToString();
        if (baseAddress != null && url.StartsWith(baseAddress))
        {
            url = url.Substring(baseAddress.Length);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        try
        {
            await EnsureSuccessfulStatusCode(response, cancellationToken);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }

    public async Task<HttpResponseMessage> GetDownloadResponse(Uri url, CancellationToken cancellationToken = default)
    {
        if (url is null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        return await GetDownloadResponse(url.ToString(), cancellationToken);
    }
}
