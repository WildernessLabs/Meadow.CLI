using Microsoft.Extensions.Logging;

namespace Meadow.Hcom
{
    public partial class MeadowDevice : IMeadowDevice
    {
        private readonly IMeadowConnection _connection;

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

        public async Task<MeadowFileInfo[]?> GetFileList(string folder, bool includeCrcs, CancellationToken? cancellationToken = null)
        {
            return await _connection.GetFileList(folder, includeCrcs, cancellationToken);
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

        public async Task<bool> WriteCoprocessorFiles(string[] localFileNames, CancellationToken? cancellationToken = null)
        {
            foreach (var file in localFileNames)
            {
                var result = await _connection.WriteCoprocessorFile(
                    file,
                    GetFileTargetAddress(file),
                    cancellationToken);

                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task UartTraceEnable(CancellationToken? cancellationToken = null)
        {
            await _connection.UartTraceEnable(cancellationToken);
        }

        public async Task UartTraceDisable(CancellationToken? cancellationToken = null)
        {
            await _connection.UartTraceDisable(cancellationToken);
        }

        public async Task UartProfilerEnable(CancellationToken? cancellationToken = null)
        {
            await _connection.UartProfilerEnable(cancellationToken);
        }

        public async Task UartProfilerDisable(CancellationToken? cancellationToken = null)
        {
            await _connection.UartProfilerDisable(cancellationToken);
        }

        private int GetFileTargetAddress(string fileName)
        {
            // TODO: determine device type so we can map the file names to target locations
            //       for now we only support the F7 so these are static and well-known

            var fn = Path.GetFileName(fileName).ToLower();
            switch (fn)
            {
                case "meadowcomms.bin":
                    return 0x10000;
                case "bootloader.bin":
                    return 0x1000;
                case "partition-table.bin":
                    return 0x8000;
                default: throw new NotSupportedException($"Unsupported coprocessor file: '{fn}'");
            }
        }

        public async Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
        {
            return await _connection.GetRtcTime(cancellationToken);
        }

        public async Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
        {
            await _connection.SetRtcTime(dateTime, cancellationToken);
        }

        public async Task TraceEnable(CancellationToken? cancellationToken = null)
        {
            await _connection.TraceEnable(cancellationToken);
        }

        public async Task TraceDisable(CancellationToken? cancellationToken = null)
        {
            await _connection.TraceDisable(cancellationToken);
        }

        public async Task SetTraceLevel(int level, CancellationToken? cancellationToken = null)
        {
            await _connection.SetTraceLevel(level, cancellationToken);
        }

        public async Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null)
        {
            await _connection.SetDeveloperParameter(parameter, value, cancellationToken);
        }

        public async Task DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null)
        {
            await _connection.DeleteFile(meadowFileName, cancellationToken);
        }

        public async Task EraseFlash(CancellationToken? cancellationToken = null)
        {
            await _connection.EraseFlash(cancellationToken);
        }

        public async Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null)
        {
            return await _connection.ReadFileString(fileName, cancellationToken);
        }

        public async Task<string> GetPublicKey(CancellationToken? cancellationToken = null)
        {
            return await _connection.GetPublicKey(cancellationToken);
        }

        public async Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken)
        {
            await _connection.StartDebugging(port, logger, cancellationToken);
        }

        public async Task SendDebuggerData(byte[] debuggerData, uint userData, CancellationToken? cancellationToken)
        {
            await _connection.SendDebuggerData(debuggerData, userData, cancellationToken);
        }
    }
}