using System.Text;

namespace Meadow.SoftwareManager.Unit.Tests;

/// <summary>
/// Minimal HTTP handler that maps absolute request URLs to canned responses.
/// Any URL without a mapping returns 404.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, byte[] Body)> _responses = new();

    public List<HttpRequestMessage> RequestMessages { get; } = new();

    public List<Uri> Requests => RequestMessages.Select(r => r.RequestUri!).ToList();

    public StubHttpMessageHandler Map(string url, HttpStatusCode status, string body = "")
    {
        return Map(url, status, Encoding.UTF8.GetBytes(body));
    }

    public StubHttpMessageHandler Map(string url, HttpStatusCode status, byte[] body)
    {
        _responses[url] = (status, body);
        return this;
    }

    public HttpClient CreateClient() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestMessages.Add(request);

        if (!_responses.TryGetValue(request.RequestUri!.ToString(), out var response))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(response.Status)
        {
            Content = new ByteArrayContent(response.Body)
        });
    }
}
