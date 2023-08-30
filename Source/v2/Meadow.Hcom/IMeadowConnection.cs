namespace Meadow.Hcom
{
    public interface IMeadowConnection
    {
        event EventHandler<(string message, string? source)> DeviceMessageReceived;
        event EventHandler<Exception> ConnectionError;
        event EventHandler<string> ConnectionMessage;
        event EventHandler<(string fileName, long completed, long total)> FileWriteProgress;

        string Name { get; }
        IMeadowDevice? Device { get; }
        Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10);
        Task WaitForMeadowAttach(CancellationToken? cancellationToken = null);
        ConnectionState State { get; }

        Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);
        Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
        Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
        Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null);
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
    }
}