using Microsoft.Extensions.Logging;

namespace Meadow.Hcom
{
    public interface IMeadowDevice
    {
        Task Reset(CancellationToken? cancellationToken = null);
        Task RuntimeDisable(CancellationToken? cancellationToken = null);
        Task RuntimeEnable(CancellationToken? cancellationToken = null);
        Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null);
        Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
        Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, string? path = null, CancellationToken? cancellationToken = null);
        Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
        Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);
        Task DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null);
        Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null);
        Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null);
        Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null);
        Task<bool> WriteCoprocessorFiles(string[] localFileNames, CancellationToken? cancellationToken = null);
        Task TraceEnable(CancellationToken? cancellationToken = null);
        Task TraceDisable(CancellationToken? cancellationToken = null);
        Task SetTraceLevel(int level, CancellationToken? cancellationToken = null);
        Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null);
        Task UartTraceEnable(CancellationToken? cancellationToken = null);
        Task UartTraceDisable(CancellationToken? cancellationToken = null);
        Task EraseFlash(CancellationToken? cancellationToken = null);
        Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null);
        Task<string> GetPublicKey(CancellationToken? cancellationToken = null);
        Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken);
        Task ForwardVisualStudioDataToMono(byte[] debuggerData, uint userData, CancellationToken? cancellationToken = default);

        MeadowDataProcessor DataProcessor { get; }
    }
}