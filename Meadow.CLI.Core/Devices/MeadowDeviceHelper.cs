using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using LibUsbDotNet.Main;

using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Meadow.CLI.Core.Logging;

namespace Meadow.CLI.Core.Devices
{
    //a simple model object that represents a meadow device including connection
    public sealed class MeadowDeviceHelper : IDisposable
    {
        private IMeadowDevice _meadowDevice;
        public TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        public readonly IMeadowLogger Logger;
        public IMeadowDevice MeadowDevice => _meadowDevice;

        public MeadowDeviceHelper(IMeadowDevice meadow, IMeadowLogger logger)
        {
            _meadowDevice = meadow;
            DeviceInfo = meadow.DeviceInfo ?? throw new ArgumentException(
                             "Device is not initialized, missing DeviceInfo",
                             nameof(meadow));
            Logger = logger;
        }

        public MeadowDeviceInfo DeviceInfo { get; private set; }

        public Task<IDictionary<string, uint>> GetFilesAndCrcsAsync(TimeSpan timeout, int partition = 0, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetFilesAndCrcsAsync(timeout, partition, cancellationToken);
        }

        public Task<IList<string>> GetFilesAndFoldersAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetFilesAndFoldersAsync(timeout, cancellationToken);
        }

        public Task<FileTransferResult> WriteFileAsync(string filename, string path, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.WriteFileAsync(filename, path, timeout, cancellationToken);
        }

        public Task DeleteFileAsync(string fileName,
                                    uint partition = 0,
                                    CancellationToken cancellationToken = default)
        {
            return _meadowDevice.DeleteFileAsync(fileName, partition, cancellationToken);
        }

        public Task EraseFlashAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.EraseFlashAsync(cancellationToken);
        }

        public Task VerifyErasedFlashAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.VerifyErasedFlashAsync(cancellationToken);
        }

        public Task FormatFileSystemAsync(uint partition = 0, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.FormatFileSystemAsync(partition, cancellationToken);
        }

        public Task RenewFileSystemAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.RenewFileSystemAsync(cancellationToken);
        }

        public async Task UpdateMonoRuntimeAsync(string fileName,
                                           uint partition = 0,
                                           CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting Mono Runtime Update");
            Logger.LogDebug("Calling Mono Disable");
            await MonoDisableAsync(cancellationToken)
                .ConfigureAwait(false);

            await ReInitializeMeadowAsync(cancellationToken);
            Trace.Assert(await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                         "Meadow was expected to have Mono Disabled");

            Logger.LogInformation("Updating Mono Runtime");

            await _meadowDevice.UpdateMonoRuntimeAsync(fileName, partition, cancellationToken).ConfigureAwait(false);
        }

        public Task WriteFileToEspFlashAsync(string fileName,
                                             uint partition = 0,
                                             string? mcuDestAddress = null,
                                             CancellationToken cancellationToken = default)
        {
            return _meadowDevice.WriteFileToEspFlashAsync(
                fileName,
                partition,
                mcuDestAddress,
                cancellationToken);
        }

        public Task FlashEspAsync(string? sourcePath = null, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.FlashEspAsync(sourcePath, cancellationToken);
        }

        public Task<MeadowDeviceInfo> GetDeviceInfoAsync(TimeSpan timeout,
                                                         CancellationToken cancellationToken =
                                                             default)
        {
            return _meadowDevice.GetDeviceInfoAsync(timeout, cancellationToken);
        }

        public Task<string?> GetDeviceNameAsync(TimeSpan timeout,
                                                CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceNameAsync(timeout, cancellationToken);
        }

        public Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetMonoRunStateAsync(cancellationToken);
        }

        public async Task MonoDisableAsync(CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false))
                && endTime > DateTime.UtcNow)
            {
                await _meadowDevice.MonoDisableAsync(cancellationToken);

                Logger.LogDebug("Re-initialize the device");
                await ReInitializeMeadowAsync(cancellationToken).ConfigureAwait(false);
            }

            if (monoRunState)
                throw new Exception("Failed to stop mono.");
        }

        public async Task MonoEnableAsync(CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false)) == false
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Enable Request");
                await _meadowDevice.MonoEnableAsync(cancellationToken)
                                   .ConfigureAwait(false);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await ReInitializeMeadowAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!monoRunState)
                throw new Exception("Failed to enable mono.");
        }

        public async Task ResetMeadowAsync(CancellationToken cancellationToken = default)
        {
            await _meadowDevice.ResetMeadowAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);
            await ReInitializeMeadowAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public Task MonoFlashAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.MonoFlashAsync(cancellationToken);
        }

        public Task EnterDfuModeAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.EnterDfuModeAsync(cancellationToken);
        }

        public Task NshEnableAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.NshEnableAsync(cancellationToken);
        }

        public Task NshDisableAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.NshDisableAsync(cancellationToken);
        }

        public Task TraceEnableAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.TraceEnableAsync(cancellationToken);
        }

        public Task SetTraceLevelAsync(uint traceLevel,
                                       CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetTraceLevelAsync(traceLevel, cancellationToken);
        }

        public Task TraceDisableAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.TraceDisableAsync(cancellationToken);
        }

        public Task QspiWriteAsync(int value, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.QspiWriteAsync(value, cancellationToken);
        }

        public Task QspiReadAsync(int value, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.QspiReadAsync(value, cancellationToken);
        }

        public Task QspiInitAsync(int value, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.QspiInitAsync(value, cancellationToken);
        }

        public async Task DeployAppAsync(string fileName, bool includePdbs = false, CancellationToken cancellationToken = default)
        {
            await MonoDisableAsync(cancellationToken)
                .ConfigureAwait(false);

            await _meadowDevice.DeployAppAsync(fileName, includePdbs, cancellationToken).ConfigureAwait(false);

            await MonoEnableAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public Task ForwardVisualStudioDataToMonoAsync(byte[] debuggerData,
                                                       uint userData,
                                                       CancellationToken cancellationToken =
                                                           default)
        {
            return _meadowDevice.ForwardVisualStudioDataToMonoAsync(
                debuggerData,
                userData,
                cancellationToken);
        }

        public async Task StartDebuggingSessionAsync(int port, CancellationToken cancellationToken)
        {
            await MonoEnableAsync(cancellationToken);

            await _meadowDevice.StartDebuggingAsync(port, cancellationToken)
                               .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            await ReInitializeMeadowAsync(cancellationToken).ConfigureAwait(false);
            if (_meadowDevice == null)
                throw new DeviceNotFoundException();

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var debuggingServer = new DebuggingServer(_meadowDevice, endpoint, Logger);

            await debuggingServer.StartListeningAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task<string?> GetInitialBytesFromFile(string fileName,
                                                     uint partition = 0,
                                                     CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetInitialBytesFromFile(fileName, partition, cancellationToken);
        }

        public Task RestartEsp32Async(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.RestartEsp32Async(cancellationToken);
        }

        public Task<string?> GetDeviceMacAddressAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceMacAddressAsync(cancellationToken);
        }

        public bool IsDeviceInitialized()
        {
            return _meadowDevice.IsDeviceInitialized();
        }

        public async Task ReInitializeMeadowAsync(CancellationToken cancellationToken = default)
        {
            var serialNumber = DeviceInfo.SerialNumber;

            _meadowDevice?.Dispose();

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            var meadow = await MeadowDeviceManager.FindMeadowBySerialNumber(
                serialNumber,
                Logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            _meadowDevice = meadow ?? throw new Exception($"Meadow not found. Serial Number {serialNumber}");
        }

        public async Task FlashOsAsync(string? runtimePath = null, bool skipRuntime = false, bool skipEsp = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (skipRuntime == false)
                {
                    await MonoDisableAsync(cancellationToken)
                        .ConfigureAwait(false);

                    // Again, verify that Mono is disabled
                    Trace.Assert(await _meadowDevice.GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                                 "Meadow was expected to have Mono Disabled");

                    await _meadowDevice.UpdateMonoRuntimeAsync(
                        runtimePath,
                        cancellationToken: cancellationToken);

                    await ReInitializeMeadowAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    Logger.LogInformation("Skipping update of runtime.");
                }

                if (skipEsp == false)
                {
                    await MonoDisableAsync(cancellationToken).ConfigureAwait(false);

                    Trace.Assert(await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                                 "Meadow was expected to have Mono Disabled");

                    Logger.LogInformation("Updating ESP");
                    await _meadowDevice.FlashEspAsync(cancellationToken)
                                       .ConfigureAwait(false);

                    // Reset the meadow again to ensure flash worked.
                    await _meadowDevice.ResetMeadowAsync(cancellationToken)
                                       .ConfigureAwait(false);

                    await ReInitializeMeadowAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    Logger.LogInformation("Skipping ESP flash");
                }

                Logger.LogInformation("Enabling Mono and Resetting");
                await MonoEnableAsync(cancellationToken);

                // This is to ensure the ESP info has updated in HCOM on the Meadow
                await Task.Delay(2000, cancellationToken)
                          .ConfigureAwait(false);

                // TODO: Verify that the device info returns the expected version
                var deviceInfo = await _meadowDevice
                                       .GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken)
                                       .ConfigureAwait(false);

                Logger.LogInformation(
                    $"Updated Meadow to OS: {deviceInfo.MeadowOsVersion} ESP: {deviceInfo.CoProcessorOs}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error flashing OS to Meadow");
            }
        }

        public static async Task<string> DfuFlashAsync(string serialPortName, string osPath, IMeadowLogger logger, CancellationToken cancellationToken = default)
        {
            var dfuAttempts = 0;

            UsbRegistry dfuDevice;
            while (true)
            {
                try
                {
                    try
                    {
                        dfuDevice = DfuUtils.GetDevice();
                        break;
                    }
                    catch (MultipleDfuDevicesException)
                    {
                        // This is bad, we can't just blindly flash with multiple devices, let the user know
                        throw;
                    }
                    catch (DeviceNotFoundException)
                    {
                        // eat it.
                    }

                    // No DFU device found, lets try to set the meadow to DFU mode.
                    using var device = await MeadowDeviceManager.GetMeadowForSerialPort(serialPortName, false).ConfigureAwait(false);

                    if (device != null)
                    {
                        logger.LogInformation("Entering DFU Mode");
                        await device.EnterDfuModeAsync(cancellationToken)
                                    .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        "An exception occurred while switching device to DFU Mode. Exception: {0}",
                        ex);
                }

                switch (dfuAttempts)
                {
                    case 5:
                        logger.LogInformation(
                            "Having trouble putting Meadow in DFU Mode, please press RST button on Meadow and press enter to try again");

                        Console.ReadKey();
                        break;
                    case 10:
                        logger.LogInformation(
                            "Having trouble putting Meadow in DFU Mode, please hold BOOT button, press RST button and release BOOT button on Meadow and press enter to try again");

                        Console.ReadKey();
                        break;
                    case > 15:
                        throw new Exception(
                            "Unable to place device in DFU mode, please disconnect the Meadow, hold the BOOT button, reconnect the Meadow, release the BOOT button and try again.");
                }

                // Lets give the device a little time to settle in and get picked up
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                dfuAttempts++;
            }

            // Get the serial number so that later we can pick the right device if the system has multiple meadow plugged in
            string serialNumber = DfuUtils.GetDeviceSerial(dfuDevice);

            logger.LogInformation("Device in DFU Mode, flashing OS");
            var res = await DfuUtils.DfuFlashAsync(osPath, dfuDevice, logger).ConfigureAwait(false);
            if (res)
            {
                logger.LogInformation("Device Flashed.");
                return serialNumber;
            }
            else
            {
                throw new MeadowDeviceException("Failed to flash meadow");
            }
        }

        private void Dispose(bool disposing)
        {
            _meadowDevice?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MeadowDeviceHelper()
        {
            Dispose(false);
        }
    }
}