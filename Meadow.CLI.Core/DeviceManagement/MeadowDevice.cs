using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowComms.RecvClasses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.CLI.Core.DeviceManagement
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

        public IDictionary<string, uint> FilesOnDevice { get; protected set; } =
            new Dictionary<string, uint>();

        public abstract Task<IDictionary<string, uint>> GetFilesAndCrcsAsync(
            int timeoutInMs = 10000,
            int partition = 0,
            CancellationToken cancellationToken = default);

        public abstract Task<bool> WriteFileAsync(string filename,
                                             string path,
                                             int timeoutInMs = 200000,
                                             CancellationToken cancellationToken = default);

        public abstract Task DeleteFileAsync(string fileName,
                                        int partition = 0,
                                        CancellationToken cancellationToken = default);

        public abstract Task EraseFlashAsync(CancellationToken cancellationToken = default);

        public abstract Task VerifyErasedFlashAsync(CancellationToken cancellationToken = default);

        public abstract Task PartitionFileSystemAsync(int numberOfPartitions = 2,
                                                 CancellationToken cancellationToken = default);

        public abstract Task MountFileSystemAsync(int partition = 0,
                                             CancellationToken cancellationToken = default);

        public abstract Task InitializeFileSystemAsync(int partition = 0,
                                                  CancellationToken cancellationToken = default);

        public abstract Task CreateFileSystemAsync(int partition = 0,
                                              CancellationToken cancellationToken = default);

        public abstract Task FormatFileSystemAsync(int partition = 0,
                                              CancellationToken cancellationToken = default);

        // TODO: Should this also take a partition parameter?
        public abstract Task RenewFileSystemAsync(CancellationToken cancellationToken = default);

        public abstract Task UpdateMonoRuntimeAsync(string fileName,
                                               string? targetFileName = null,
                                               int partition = 0,
                                               CancellationToken cancellationToken = default);

        public abstract Task WriteFileToEspFlashAsync(string fileName,
                                                 string? targetFileName = null,
                                                 int partition = 0,
                                                 string? mcuDestAddr = null,
                                                 CancellationToken cancellationToken = default);

        public abstract Task FlashEspAsync(string sourcePath,
                                      CancellationToken cancellationToken = default);

        public abstract Task<string?> GetDeviceInfoAsync(int timeoutInMs = 5000,
                                                    CancellationToken cancellationToken = default);

        public abstract Task<string?> GetDeviceNameAsync(int timeoutInMs = 5000,
                                                    CancellationToken cancellationToken = default);

        public abstract Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default);
        public abstract Task MonoDisableAsync(CancellationToken cancellationToken = default);
        public abstract Task MonoEnableAsync(CancellationToken cancellationToken = default);
        public abstract Task ResetMeadowAsync(CancellationToken cancellationToken = default);
        public abstract Task MonoFlashAsync(CancellationToken cancellationToken = default);
        public abstract Task EnterDfuModeAsync(CancellationToken cancellationToken = default);
        public abstract Task NshEnableAsync(CancellationToken cancellationToken = default);
        public abstract Task NshDisableAsync(CancellationToken cancellationToken = default);
        public abstract Task TraceEnableAsync(CancellationToken cancellationToken = default);
        public abstract Task TraceDisableAsync(CancellationToken cancellationToken = default);
        public abstract Task QspiWriteAsync(int value, CancellationToken cancellationToken = default);
        public abstract Task QspiReadAsync(int value, CancellationToken cancellationToken = default);
        public abstract Task QspiInitAsync(int value, CancellationToken cancellationToken = default);
        public abstract Task<string> GetInitialFileDataAsync(string fileName, int timeoutInMs, CancellationToken cancellationToken = default);
        public abstract Task DeployAppAsync(string fileName, CancellationToken cancellationToken = default);

        public abstract Task ForwardVisualStudioDataToMonoAsync(byte[] debuggerData,
                                                           int userData,
                                                           CancellationToken cancellationToken =
                                                               default);

        public virtual async Task FlashEspAsync(CancellationToken cancellationToken = default)
        {
            await WaitForReadyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await MonoDisableAsync(cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await GetMonoRunStateAsync(cancellationToken)
                    .ConfigureAwait(false),
                "Meadow was expected to have Mono Disabled");

            Logger.LogInformation("Flashing ESP");

            Logger.LogInformation($"Transferring {DownloadManager.NetworkMeadowCommsFilename}");

            await WriteFileToEspFlashAsync(
                    Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.NetworkMeadowCommsFilename),
                    mcuDestAddr: "0x10000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Logger.LogInformation($"Transferring {DownloadManager.NetworkBootloaderFilename}");

            await WriteFileToEspFlashAsync(
                    Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.NetworkBootloaderFilename),
                    mcuDestAddr: "0x1000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Logger.LogInformation($"Transferring {DownloadManager.NetworkPartitionTableFilename}");

            await WriteFileToEspFlashAsync(
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
        public virtual async Task WaitForReadyAsync(int timeout = 60_000,
                                               CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var then = now.AddMilliseconds(timeout);
            while (DateTime.UtcNow < then)
            {
                try
                {
                    var deviceInfo = await GetDeviceInfoAsync(cancellationToken: cancellationToken);

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

        public virtual Task ForwardMonoDataToVisualStudioAsync(byte[] debuggerData,
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