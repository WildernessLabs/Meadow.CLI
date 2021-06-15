using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.Devices
{
    public interface IMeadowDevice : IDisposable
    {
        public ILogger Logger { get; }
        public MeadowDataProcessor DataProcessor { get; }
        public MeadowDeviceInfo? DeviceInfo { get; }

        public IDictionary<string, uint> FilesOnDevice { get; }
        public Task<IDictionary<string, uint>> GetFilesAndCrcsAsync(TimeSpan timeout, int partition = 0, CancellationToken cancellationToken = default);
        public Task<FileTransferResult> WriteFileAsync(string filename, string path, TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task DeleteFileAsync(string fileName, uint partition = 0, CancellationToken cancellationToken = default);
        public Task EraseFlashAsync(CancellationToken cancellationToken = default);
        public Task VerifyErasedFlashAsync(CancellationToken cancellationToken = default);
        public Task FormatFileSystemAsync(uint partition = 0, CancellationToken cancellationToken = default);
        public Task RenewFileSystemAsync(CancellationToken cancellationToken = default);
        public Task UpdateMonoRuntimeAsync(string? fileName, uint partition = 0, CancellationToken cancellationToken = default);
        public Task WriteFileToEspFlashAsync(string fileName, uint partition = 0, string? mcuDestAddress = null, CancellationToken cancellationToken = default);
        public Task FlashEspAsync(string? sourcePath, CancellationToken cancellationToken = default);
        public Task<MeadowDeviceInfo> GetDeviceInfoAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task<string?> GetDeviceNameAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default);
        public Task MonoDisableAsync(CancellationToken cancellationToken = default);
        public Task MonoEnableAsync(CancellationToken cancellationToken = default);
        public Task ResetMeadowAsync(CancellationToken cancellationToken = default);
        public Task MonoFlashAsync(CancellationToken cancellationToken = default);
        public Task EnterDfuModeAsync(CancellationToken cancellationToken = default);
        public Task NshEnableAsync(CancellationToken cancellationToken = default);
        public Task NshDisableAsync(CancellationToken cancellationToken = default);
        public Task TraceEnableAsync(CancellationToken cancellationToken = default);
        public Task SetTraceLevelAsync(uint traceLevel, CancellationToken cancellationToken = default);
        public Task TraceDisableAsync(CancellationToken cancellationToken = default);
        public Task QspiWriteAsync(int value, CancellationToken cancellationToken = default);
        public Task QspiReadAsync(int value, CancellationToken cancellationToken = default);
        public Task QspiInitAsync(int value, CancellationToken cancellationToken = default);
        public Task DeployAppAsync(string fileName, bool includePdbs = false, CancellationToken cancellationToken = default);
        public Task ForwardVisualStudioDataToMonoAsync(byte[] debuggerData, uint userData, CancellationToken cancellationToken = default);
        public Task StartDebuggingAsync(int port, CancellationToken cancellationToken);
        public Task<string?> GetInitialBytesFromFile(string fileName, uint partition = 0, CancellationToken cancellationToken = default);
        public Task RestartEsp32Async(CancellationToken cancellationToken = default);
        public Task<string?> GetDeviceMacAddressAsync(CancellationToken cancellationToken = default);
        public Task<bool> InitializeAsync(CancellationToken cancellationToken);
        public bool IsDeviceInitialized();
        public Task FlashEspAsync(CancellationToken cancellationToken = default);
        public Task<bool> IsFileOnDevice(string filename, CancellationToken cancellationToken = default);
    }
}
