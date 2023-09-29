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

    public override async Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null)
    {
        var response = await _client.GetAsync($"{_baseUri}/api/info");

        if (response.IsSuccessStatusCode)
        {
            var r = JsonSerializer.Deserialize<DeviceInfoHttpResponse>(await response.Content.ReadAsStringAsync());
            return new DeviceInfo(r.ToDictionary());
        }
        else
        {
            throw new Exception($"API responded with {response.StatusCode}");
        }
    }

    public override Task WaitForMeadowAttach(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task ResetDevice(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task RuntimeDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task RuntimeEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteCoprocessorFile(string localFileName, int destinationAddress, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task TraceEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task TraceDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetTraceLevel(int level, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task UartTraceEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task UartTraceDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task EraseFlash(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }
}