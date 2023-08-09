using System.Text.Json;

namespace Meadow.Hcom;

public class TcpConnection : ConnectionBase
{
    private HttpClient _client;
    private string _baseUri;

    public override string Name => _baseUri;

    public TcpConnection(string uri)
    {
        _baseUri = uri;
        _client = new HttpClient();
    }

    public override async Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10)
    {
        /*
        var request = RequestBuilder.Build<GetDeviceInfoRequest>();

        base.EnqueueRequest(request);

        // get the info and "attach"
        var timeout = timeoutSeconds * 2;

        while (timeout-- > 0)
        {
            if (cancellationToken?.IsCancellationRequested ?? false) return null;
            if (timeout <= 0) throw new TimeoutException();

            // do we have a device info?

            if (State == ConnectionState.MeadowAttached)
            {
                break;
            }

            await Task.Delay(500);
        }
        */

        // TODO: is there a way to "attach"?  ping result? device info?
        return Device = new MeadowDevice(this);

        // TODO: web socket for listen?
    }

    internal override async Task DeliverRequest(IRequest request)
    {
        if (request is GetDeviceInfoRequest)
        {
            try
            {
                var response = await _client.GetAsync($"{_baseUri}/api/info");

                if (response.IsSuccessStatusCode)
                {
                    var r = JsonSerializer.Deserialize<DeviceInfoHttpResponse>(await response.Content.ReadAsStringAsync());
                    var d = r.ToDictionary();

                    foreach (var listener in ConnectionListeners)
                    {
                        listener.OnDeviceInformationMessageReceived(d);
                    }
                }
                else
                {
                    RaiseConnectionError(new Exception($"API responded with {response.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                RaiseConnectionError(ex);
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public Task WaitForMeadowAttach(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }
}