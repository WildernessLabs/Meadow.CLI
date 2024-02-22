namespace Meadow.Hcom;

public interface IMeadowConnection : IDisposable
{
    event EventHandler<(string message, string? source)> DeviceMessageReceived;
    event EventHandler<Exception> ConnectionError;
    event EventHandler<string> ConnectionMessage;
    event EventHandler<(string fileName, long completed, long total)> FileWriteProgress;
    event EventHandler FileWriteFailed;
    event EventHandler<byte[]> DebuggerMessageReceived;

    string Name { get; }
    IMeadowDevice? Device { get; }
    Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10);
    void Detach();
    Task WaitForMeadowAttach(CancellationToken? cancellationToken = null);
    ConnectionState State { get; }

    Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);
    Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
    Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null);
    Task<bool> DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null);
    Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
    Task<MeadowFileInfo[]?> GetFileList(string folder, bool includeCrcs, CancellationToken? cancellationToken = null);
    Task ResetDevice(CancellationToken? cancellationToken = null);
    Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null);
    Task RuntimeDisable(CancellationToken? cancellationToken = null);
    Task RuntimeEnable(CancellationToken? cancellationToken = null);
    Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null);
    Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null);

    Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null);
    Task<bool> WriteCoprocessorFile(string localFileName, int destinationAddress, CancellationToken? cancellationToken = null);

    Task TraceEnable(CancellationToken? cancellationToken = null);
    Task TraceDisable(CancellationToken? cancellationToken = null);
    Task SetTraceLevel(int level, CancellationToken? cancellationToken = null);

    Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null);

    Task UartTraceEnable(CancellationToken? cancellationToken = null);
    Task UartTraceDisable(CancellationToken? cancellationToken = null);
    Task UartProfilerEnable(CancellationToken? cancellationToken = null);
    Task UartProfilerDisable(CancellationToken? cancellationToken = null);


    Task EraseFlash(CancellationToken? cancellationToken = null);
    Task<string> GetPublicKey(CancellationToken? cancellationToken = null);
    Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken);
    Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken);
    Task SendDebuggerData(byte[] debuggerData, uint userData, CancellationToken? cancellationToken);
    Task DeployApp(string folderPath, bool isDebugging, ILogger? logger, CancellationToken? cancellationToken);
}