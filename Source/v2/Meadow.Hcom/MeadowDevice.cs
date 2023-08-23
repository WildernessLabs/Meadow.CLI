namespace Meadow.Hcom
{
    public partial class MeadowDevice : IMeadowDevice
    {
        private IMeadowConnection _connection;

        internal MeadowDevice(IMeadowConnection connection)
        {
            _connection = connection;
        }

        public async Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null)
        {
            return await _connection.IsRuntimeEnabled(cancellationToken);
        }

        public async Task Reset(CancellationToken? cancellationToken = null)
        {
            await _connection.ResetDevice(cancellationToken);
        }

        public async Task RuntimeDisable(CancellationToken? cancellationToken = null)
        {
            await _connection.RuntimeDisable(cancellationToken);
        }

        public async Task RuntimeEnable(CancellationToken? cancellationToken = null)
        {
            await _connection.RuntimeEnable(cancellationToken);
        }

        public async Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null)
        {
            return await _connection.GetDeviceInfo(cancellationToken);
        }

        public async Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null)
        {
            return await _connection.GetFileList(includeCrcs, cancellationToken);
        }

        public async Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
        {
            return await _connection.ReadFile(meadowFileName, localFileName, cancellationToken);
        }

        public async Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null)
        {
            return await _connection.WriteFile(localFileName, meadowFileName, cancellationToken);
        }

        public async Task<bool> WriteRuntime(string localFileName, CancellationToken? cancellationToken = null)
        {
            return await _connection.WriteRuntime(localFileName, cancellationToken);
        }

        public async Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
        {
            return await _connection.GetRtcTime(cancellationToken);
        }

        public async Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
        {
            await _connection.SetRtcTime(dateTime, cancellationToken);
        }

        public Task FlashOS(string requestedversion, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task FlashCoprocessor(string requestedversion, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }
    }
}