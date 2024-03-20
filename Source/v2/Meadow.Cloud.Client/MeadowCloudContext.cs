using System.Net.Http.Headers;

namespace Meadow.Cloud.Client;

public class MeadowCloudContext
{
    public HttpClient HttpClient { get; }
    public AuthenticationHeaderValue? Authorization { get; set; }
    public Uri BaseAddress { get; set; }
    public MeadowCloudUserAgent UserAgent { get; set; }

    public MeadowCloudContext(HttpClient httpClient, Uri baseAddress, MeadowCloudUserAgent userAgent)
    {
        HttpClient = httpClient;
        BaseAddress = baseAddress;
        UserAgent = userAgent;
    }

    public MeadowCloudContext(HttpClient httpClient, MeadowCloudUserAgent userAgent)
        : this(httpClient, MeadowCloudClient.DefaultHostUri, userAgent)
    {
    }
}
