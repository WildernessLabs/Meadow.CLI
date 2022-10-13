using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CLI.Core.Devices
{
    public interface IMeadowDevice : IDisposable
    {
        public ILogger Logger { get; }
        public MeadowDataProcessor DataProcessor { get; }
        public MeadowDeviceInfo? DeviceInfo { get; }

        public IDictionary<string, uint> FilesOnDevice { get; }
        public Task<IList<string>> GetFilesAndFolders(TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task<IDictionary<string, uint>> GetFilesAndCrcs(TimeSpan timeout, int partition = 0, CancellationToken cancellationToken = default);
        public Task<FileTransferResult> WriteFile(string filename, string path, TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task DeleteFile(string fileName, uint partition = 0, CancellationToken cancellationToken = default);
        public Task EraseFlash(CancellationToken cancellationToken = default);
        public Task VerifyErasedFlash(CancellationToken cancellationToken = default);
        public Task FormatFileSystem(uint partition = 0, CancellationToken cancellationToken = default);
        public Task RenewFileSystem(CancellationToken cancellationToken = default);
        public Task UpdateMonoRuntime(string? fileName, uint partition = 0, CancellationToken cancellationToken = default);
        public Task UpdateMonoRuntime(string? fileName, string? osVersion, uint partition = 0, CancellationToken cancellationToken = default);
        public Task WriteFileToEspFlash(string? fileName, uint partition = 0, string? mcuDestAddress = null, CancellationToken cancellationToken = default);

        public Task FlashEsp(string? sourcePath, string? osVersion = null, CancellationToken cancellationToken = default);
        
        public Task<MeadowDeviceInfo> GetDeviceInfo(TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task<string?> GetDeviceName(TimeSpan timeout, CancellationToken cancellationToken = default);
        public Task<bool> GetMonoRunState(CancellationToken cancellationToken = default);
        public Task MonoDisable(CancellationToken cancellationToken = default);
        public Task MonoEnable(CancellationToken cancellationToken = default);
        public Task ResetMeadow(CancellationToken cancellationToken = default);
        public Task FlashMonoRuntime(CancellationToken cancellationToken = default);
        public Task EnterDfuMode(CancellationToken cancellationToken = default);
        public Task NshEnable(CancellationToken cancellationToken = default);
        public Task NshDisable(CancellationToken cancellationToken = default);
        public Task TraceEnable(CancellationToken cancellationToken = default);
        public Task SetTraceLevel(uint traceLevel, CancellationToken cancellationToken = default);
        public Task SetDeveloper1(uint userData, CancellationToken cancellationToken = default);
        public Task SetDeveloper2(uint userData, CancellationToken cancellationToken = default);
        public Task SetDeveloper3(uint userData, CancellationToken cancellationToken = default);
        public Task SetDeveloper4(uint userData, CancellationToken cancellationToken = default);
        public Task Uart1Apps(CancellationToken cancellationToken = default);
        public Task Uart1Trace(CancellationToken cancellationToken = default);
        public Task TraceDisable(CancellationToken cancellationToken = default);
        public Task QspiWrite(int value, CancellationToken cancellationToken = default);
        public Task QspiRead(int value, CancellationToken cancellationToken = default);
        public Task QspiInit(int value, CancellationToken cancellationToken = default);
        public Task DeployApp(string fileName, string osVersion, bool includePdbs = false, CancellationToken cancellationToken = default);
        public Task ForwardVisualStudioDataToMono(byte[] debuggerData, uint userData, CancellationToken cancellationToken = default);
        public Task StartDebugging(int port, CancellationToken cancellationToken);
        public Task<string?> GetInitialBytesFromFile(string fileName, uint partition = 0, CancellationToken cancellationToken = default);
        public Task RestartEsp32(CancellationToken cancellationToken = default);
        public Task<string?> GetDeviceMacAddress(CancellationToken cancellationToken = default);
        public Task<bool> Initialize(CancellationToken cancellationToken);
        public bool IsDeviceInitialized();
        public Task<bool> IsFileOnDevice(string filename, CancellationToken cancellationToken = default);
    }
}
