namespace Meadow.Hcom
{
    public interface IMeadowDevice
    {
        Task Reset(CancellationToken? cancellationToken = null);
        Task RuntimeDisable(CancellationToken? cancellationToken = null);
        Task RuntimeEnable(CancellationToken? cancellationToken = null);
        Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null);
        Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
        Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null);
        Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
        Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);

        Task FlashOS(string requestedversion, CancellationToken? cancellationToken = null);
        Task FlashCoprocessor(string requestedversion, CancellationToken? cancellationToken = null);
        Task FlashRuntime(string requestedversion, CancellationToken? cancellationToken = null);
        Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null);
        Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null);
    }
}