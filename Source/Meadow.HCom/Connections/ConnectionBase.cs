﻿namespace Meadow.Hcom;

public abstract class ConnectionBase : IMeadowConnection, IDisposable
{
    private bool _isDisposed;

    public virtual ConnectionState State { get; protected set; }
    public IMeadowDevice? Device { get; protected set; }

    public event EventHandler<(string message, string? source)> DeviceMessageReceived = default!;
    public event EventHandler<Exception> ConnectionError = default!;
    public event EventHandler<(string fileName, long completed, long total)> FileWriteProgress = default!;
    public event EventHandler<string> ConnectionMessage = default!;
    public event EventHandler FileWriteFailed = default!;
    public event EventHandler<byte[]> DebuggerMessageReceived;
    public event EventHandler<string>? FileReadCompleted = default!;
    public event EventHandler<int>? FileBytesReceived;

    public abstract string Name { get; }

    public abstract Task WaitForMeadowAttach(CancellationToken? cancellationToken = null);
    public abstract Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10);
    public abstract void Detach();
    public abstract Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
    public abstract Task<MeadowFileInfo[]?> GetFileList(string folder, bool includeCrcs, CancellationToken? cancellationToken = null);
    public abstract Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);
    public abstract Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
    public abstract Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null);
    public abstract Task<bool> DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null);
    public abstract Task ResetDevice(CancellationToken? cancellationToken = null);
    public abstract Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null);
    public abstract Task RuntimeDisable(CancellationToken? cancellationToken = null);
    public abstract Task RuntimeEnable(CancellationToken? cancellationToken = null);
    public abstract Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null);
    public abstract Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null);
    public abstract Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null);
    public abstract Task<bool> WriteCoprocessorFile(string localFileName, int destinationAddress, CancellationToken? cancellationToken = null);
    public abstract Task TraceEnable(CancellationToken? cancellationToken = null);
    public abstract Task TraceDisable(CancellationToken? cancellationToken = null);
    public abstract Task SetTraceLevel(int level, CancellationToken? cancellationToken = null);
    public abstract Task SetDeveloperParameter(ushort parameter, uint value, TimeSpan timeout, CancellationToken? cancellationToken = null);
    public abstract Task UartTraceEnable(CancellationToken? cancellationToken = null);
    public abstract Task UartTraceDisable(CancellationToken? cancellationToken = null);
    public abstract Task UartProfilerEnable(CancellationToken? cancellationToken = null);
    public abstract Task UartProfilerDisable(CancellationToken? cancellationToken = null);
    public abstract Task EraseFlash(CancellationToken? cancellationToken = null);
    public abstract Task<string> GetPublicKey(CancellationToken? cancellationToken = null);
    public abstract Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken);
    public abstract Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken);

    public abstract Task SendDebuggerData(byte[] debuggerData, uint userData, CancellationToken? cancellationToken);

    public abstract Task NshDisable(CancellationToken? cancellationToken = null);
    public abstract Task NshEnable(CancellationToken? cancellationToken = null);

    public ConnectionBase()
    {
    }

    protected void RaiseConnectionMessage(string message)
    {
        ConnectionMessage?.Invoke(this, message);
    }

    protected void RaiseDebuggerMessage(byte[] data)
    {
        DebuggerMessageReceived?.Invoke(this, data);
    }

    protected void RaiseFileReadCompleted(string fileName)
    {
        FileReadCompleted?.Invoke(this, fileName);
    }

    protected void RaiseFileWriteProgress(string fileName, long progress, long total)
    {
        FileWriteProgress?.Invoke(this, (fileName, progress, total));
    }

    protected void RaiseFileWriteFailed()
    {
        FileWriteFailed?.Invoke(this, EventArgs.Empty);
    }

    protected void RaiseDeviceMessageReceived(string message, string? source)
    {
        DeviceMessageReceived?.Invoke(this, (message, source));
    }

    protected void RaiseConnectionError(Exception error)
    {
        ConnectionError?.Invoke(this, error);
    }

    protected void RaiseFileBytesReceived(int length)
    {
        FileBytesReceived?.Invoke(this, length);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
            }

            _isDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}