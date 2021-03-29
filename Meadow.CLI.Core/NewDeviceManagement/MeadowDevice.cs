using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.NewDeviceManagement.MeadowComms.RecvClasses;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    //is this needed?
    public class MeadowDeviceException : Exception
    {
        public MeadowDeviceException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    //a simple model object that represents a meadow device including connection
    public abstract class MeadowDevice : IDisposable
    {
        public readonly MeadowDataProcessor DataProcessor;
        private protected DebuggingServer DebuggingServer;

        protected MeadowDevice(MeadowDataProcessor dataProcessor)
        {
            DataProcessor = dataProcessor;
        }

        public MeadowDeviceInfo DeviceInfo { get; protected set; }

        public IDictionary<string, UInt32> FilesOnDevice { get; protected set; } =
            new Dictionary<string, uint>();

        public abstract Task<IDictionary<string, UInt32>> GetFilesAndCrcs(
            int timeoutInMs = 10000,
            int partition = 0,
            CancellationToken cancellationToken = default);

        public abstract Task<bool> WriteFile(string filename,
                                             string path,
                                             int timeoutInMs = 200000,
                                             CancellationToken cancellationToken = default);

        public abstract Task DeleteFile(string fileName,
                                        int partition = 0,
                                        CancellationToken cancellationToken = default);

        public abstract Task EraseFlash(CancellationToken cancellationToken = default);

        public abstract Task VerifyErasedFlash(CancellationToken cancellationToken = default);

        public abstract Task PartitionFileSystem(int numberOfPartitions = 2,
                                                 CancellationToken cancellationToken = default);

        public abstract Task MountFileSystem(int partition = 0,
                                             CancellationToken cancellationToken = default);

        public abstract Task InitializeFileSystem(int partition = 0,
                                                  CancellationToken cancellationToken = default);

        public abstract Task CreateFileSystem(int partition = 0,
                                              CancellationToken cancellationToken = default);

        public abstract Task FormatFileSystem(int partition = 0,
                                              CancellationToken cancellationToken = default);

        public abstract Task UpdateMonoRuntime(string fileName,
                                               string? targetFileName = null,
                                               int partition = 0,
                                               CancellationToken cancellationToken = default);

        public abstract Task WriteFileToEspFlash(string fileName,
                                                 string? targetFileName = null,
                                                 int partition = 0,
                                                 string? mcuDestAddr = null,
                                                 CancellationToken cancellationToken = default);

        public abstract Task FlashEsp(string sourcePath,
                                      CancellationToken cancellationToken = default);

        public abstract Task<string> GetDeviceInfo(int timeoutInMs = 5000,
                                                   CancellationToken cancellationToken = default);

        public abstract Task<string> GetDeviceName(int timeoutInMs = 5000,
                                                   CancellationToken cancellationToken = default);

        public abstract Task<bool> GetMonoRunState(CancellationToken cancellationToken = default);

        public abstract Task MonoDisable(CancellationToken cancellationToken = default);

        public abstract Task MonoEnable(CancellationToken cancellationToken = default);

        public abstract Task ResetMeadow(CancellationToken cancellationToken = default);

        public abstract Task EnterDfuMode(CancellationToken cancellationToken = default);

        public abstract Task ForwardVisualStudioDataToMono(
            byte[] debuggerData,
            int userData,
            CancellationToken cancellationToken = default);

        public virtual Task ForwardMonoDataToVisualStudio(byte[] debuggerData, CancellationToken cancellationToken = default)
        {
            return DebuggingServer.SendToVisualStudio(debuggerData, cancellationToken);
        }

        public ValueTask<bool> IsFileOnDevice(string filename)
        {
            return new(FilesOnDevice.ContainsKey(filename));
        }

        public abstract bool IsDeviceInitialized();

        public abstract void Dispose();
    }
}