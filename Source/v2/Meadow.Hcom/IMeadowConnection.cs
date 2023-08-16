namespace Meadow.Hcom
{
    public interface IMeadowConnection
    {
        event EventHandler<Exception> ConnectionError;

        string Name { get; }
        IMeadowDevice? Device { get; }
        Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10);
        Task WaitForMeadowAttach(CancellationToken? cancellationToken = null);
        ConnectionState State { get; }

        // internal stuff that probably needs to get moved to anotehr interface
        void AddListener(IConnectionListener listener);
        void RemoveListener(IConnectionListener listener);
        void EnqueueRequest(IRequest command);





        Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);
        Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
        Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
        Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null);
        Task Reset(CancellationToken? cancellationToken = null);
        Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null);
        Task RuntimeDisable(CancellationToken? cancellationToken = null);
        Task RuntimeEnable(CancellationToken? cancellationToken = null);
        Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null);
        Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null);
    }
}