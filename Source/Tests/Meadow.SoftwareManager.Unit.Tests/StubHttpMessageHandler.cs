using System.Text;

namespace Meadow.SoftwareManager.Unit.Tests;

/// <summary>
/// Minimal HTTP handler that maps absolute request URLs to canned responses.
/// Any URL without a mapping returns 404.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new();

    public List<Uri> Requests { get; } = new();

    public StubHttpMessageHandler Map(string url, HttpStatusCode status, string body = "")
    {
        _responses[url] = (status, body);
        return this;
    }

    public HttpClient CreateClient() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);

        if (!_responses.TryGetValue(request.RequestUri!.ToString(), out var response))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(response.Status)
        {
            Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
        });
    }
}
