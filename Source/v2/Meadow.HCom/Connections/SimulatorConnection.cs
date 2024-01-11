using Microsoft.Extensions.Logging;

namespace Meadow.Hcom;

public class SimulatorConnection : ConnectionBase
{
    public override string Name => "Simulator";

    private HttpClient? _client = null;

    public override Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10)
    {
        // TODO: use some config our environment variable to launch the simulator process if it's not running

        _client = new HttpClient();

        throw new NotImplementedException();
    }

    public override Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null)
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

    public override Task<bool> DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task EraseFlash(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<MeadowFileInfo[]?> GetFileList(string folder, bool includeCrcs, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<string> GetPublicKey(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task ResetDevice(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task SetTraceLevel(int level, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task TraceDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task TraceEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task UartTraceDisable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task UartTraceEnable(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task WaitForMeadowAttach(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteCoprocessorFile(string localFileName, int destinationAddress, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }

    public override Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
    }
}
