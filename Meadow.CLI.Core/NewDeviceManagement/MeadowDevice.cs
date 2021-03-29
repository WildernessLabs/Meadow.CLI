using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.NewDeviceManagement.MeadowComms.RecvClasses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        private protected readonly ILogger Logger;

        protected MeadowDevice(MeadowDataProcessor dataProcessor, ILogger? logger)
        {
            DataProcessor = dataProcessor;
            Logger = logger ?? new NullLogger<MeadowDevice>();
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

        // TODO: Should this also take a partition parameter?
        public abstract Task RenewFileSystem(CancellationToken cancellationToken = default);

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

        public abstract Task<string?> GetDeviceInfo(int timeoutInMs = 5000,
                                                    CancellationToken cancellationToken = default);

        public abstract Task<string?> GetDeviceName(int timeoutInMs = 5000,
                                                    CancellationToken cancellationToken = default);

        public abstract Task<bool> GetMonoRunState(CancellationToken cancellationToken = default);
        public abstract Task MonoDisable(CancellationToken cancellationToken = default);
        public abstract Task MonoEnable(CancellationToken cancellationToken = default);
        public abstract Task ResetMeadow(CancellationToken cancellationToken = default);
        public abstract Task MonoFlash(CancellationToken cancellationToken = default);
        public abstract Task EnterDfuMode(CancellationToken cancellationToken = default);
        public abstract Task NshEnable(CancellationToken cancellationToken = default);
        public abstract Task NshDisable(CancellationToken cancellationToken = default);
        public abstract Task TraceEnable(CancellationToken cancellationToken = default);
        public abstract Task TraceDisable(CancellationToken cancellationToken = default);
        public abstract Task QspiWrite(int value, CancellationToken cancellationToken = default);
        public abstract Task QspiRead(int value, CancellationToken cancellationToken = default);
        public abstract Task QspiInit(int value, CancellationToken cancellationToken = default);

        public abstract Task ForwardVisualStudioDataToMono(byte[] debuggerData,
                                                           int userData,
                                                           CancellationToken cancellationToken =
                                                               default);

        public virtual async Task FlashEsp(CancellationToken cancellationToken = default)
        {
            await WaitForReady(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await MonoDisable(cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await GetMonoRunState(cancellationToken)
                    .ConfigureAwait(false),
                "Meadow was expected to have Mono Disabled");

            Logger.LogInformation("Flashing ESP");

            Logger.LogInformation($"Transferring {DownloadManager.NetworkMeadowCommsFilename}");

            await WriteFileToEspFlash(
                    Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.NetworkMeadowCommsFilename),
                    mcuDestAddr: "0x10000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Logger.LogInformation($"Transferring {DownloadManager.NetworkBootloaderFilename}");

            await WriteFileToEspFlash(
                    Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.NetworkBootloaderFilename),
                    mcuDestAddr: "0x1000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Logger.LogInformation($"Transferring {DownloadManager.NetworkPartitionTableFilename}");

            await WriteFileToEspFlash(
                    Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.NetworkPartitionTableFilename),
                    mcuDestAddr: "0x8000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);
        }

        /// <summary>
        /// Wait for the Meadow to respond to commands
        /// </summary>
        /// <param name="timeout">How long to wait for the meadow to become ready</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> indicating if the Meadow is ready</returns>
        public virtual async Task WaitForReady(int timeout = 60_000,
                                               CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var then = now.AddMilliseconds(timeout);
            while (DateTime.UtcNow < then)
            {
                try
                {
                    var deviceInfo = await GetDeviceInfo(cancellationToken: cancellationToken);

                    if (string.IsNullOrWhiteSpace(deviceInfo) == false)
                        return;
                }
                catch (MeadowCommandException meadowCommandException)
                {
                    Logger.LogTrace(meadowCommandException.ToString());
                }
                catch (Exception ex)
                {
                    Logger.LogTrace($"An exception occurred. Retrying. Exception: {ex}");
                }

                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);
            }

            throw new Exception($"Device not ready after {timeout}ms");
        }

        public virtual Task ForwardMonoDataToVisualStudio(byte[] debuggerData,
                                                          CancellationToken cancellationToken =
                                                              default)
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