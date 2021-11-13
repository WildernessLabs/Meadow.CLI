﻿using System;
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

namespace Meadow.CLI.Core.Devices
{
    //a simple model object that represents a meadow device including connection
    public sealed class MeadowDeviceHelper : IDisposable
    {
        private IMeadowDevice _meadowDevice;
        public TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        public readonly ILogger Logger;
        public IMeadowDevice MeadowDevice => _meadowDevice;

        public MeadowDeviceHelper(IMeadowDevice meadow, ILogger logger)
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

        public Task<FileTransferResult> WriteFileAsync(string sourceFileName, string destinationFileName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.WriteFileAsync(sourceFileName, destinationFileName, timeout, cancellationToken);
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

        public async Task UpdateMonoRuntimeAsync(string fileName, uint partition = 0, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting Mono Runtime Update");
            Logger.LogDebug("Calling Mono Disable");
            await MonoDisableAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await ReInitializeMeadowAsync(cancellationToken);
            Trace.Assert(await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                         "Meadow was expected to have Mono Disabled");

            Logger.LogInformation("Updating Mono Runtime");

            await _meadowDevice.UpdateMonoRuntimeAsync(fileName, partition, cancellationToken).ConfigureAwait(false);
        }

        public Task WriteFileToEspFlashAsync(string fileName, uint partition = 0, string? mcuDestAddress = null, CancellationToken cancellationToken = default)
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

        public Task<MeadowDeviceInfo> GetDeviceInfoAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceInfoAsync(timeout, cancellationToken);
        }

        public Task<string?> GetDeviceNameAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceNameAsync(timeout, cancellationToken);
        }

        public Task<bool> GetMonoRunStateAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetMonoRunStateAsync(cancellationToken);
        }

        public async Task MonoDisableAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false)) || force
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Disable Request (Forced? {forced})", force);
                await _meadowDevice.MonoDisableAsync(cancellationToken);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(3000, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await ReInitializeMeadowAsync(cancellationToken).ConfigureAwait(false);
                force = false;
            }

            if (monoRunState)
                throw new Exception("Failed to stop mono.");
        }

        public async Task MonoEnableAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState;
            while ((monoRunState = await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false)) == false || force
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Enable Request (Forced? {forced})", force);
                await _meadowDevice.MonoEnableAsync(cancellationToken)
                                   .ConfigureAwait(false);

                Logger.LogDebug("Waiting for Meadow to cycle");
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                Logger.LogDebug("Re-initialize the device");
                await ReInitializeMeadowAsync(cancellationToken).ConfigureAwait(false);
                force = false;
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

        public Task SetTraceLevelAsync(uint traceLevel, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetTraceLevelAsync(traceLevel, cancellationToken);
        }

        public Task TraceDisableAsync(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.TraceDisableAsync(cancellationToken);
        }

        public Task SetDeveloper1(uint userData, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetDeveloper1(userData, cancellationToken);
        }

        public Task SetDeveloper2(uint userData, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetDeveloper2(userData, cancellationToken);
        }

        public Task SetDeveloper3(uint userData, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetDeveloper3(userData, cancellationToken);
        }

        public Task SetDeveloper4(uint userData, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetDeveloper4(userData, cancellationToken);
        }

        public Task Uart1Apps(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.Uart1Apps(cancellationToken);
        }

        public Task Uart1Trace(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.Uart1Trace(cancellationToken);
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

        public async Task DeployAppAsync(string fileName, bool includePdbs = true, CancellationToken cancellationToken = default)
        {
            await MonoDisableAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            //check the device OS version, in order to download matching assemblies to it
            var deviceInfo = await _meadowDevice.GetDeviceInfoAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            string osVersion = deviceInfo.MeadowOsVersion.Split(' ')[0]; // we want the first part of e.g. '0.5.3.0 (Oct 13 2021 13:39:12)'

            await _meadowDevice.DeployAppAsync(fileName, osVersion, includePdbs, cancellationToken).ConfigureAwait(false);

            await MonoEnableAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public Task ForwardVisualStudioDataToMonoAsync(byte[] debuggerData, uint userData, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.ForwardVisualStudioDataToMonoAsync(
                debuggerData,
                userData,
                cancellationToken);
        }

        /// <summary>
        /// Start a session to debug an application on the Meadow
        /// </summary>
        /// <param name="port">The port to use for the debugging proxy</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for cancelling the operation</param>
        /// <returns>A running <see cref="DebuggingServer"/> that is available for connections</returns>
        public async Task<DebuggingServer> StartDebuggingSessionAsync(int port, CancellationToken cancellationToken)
        {
            await MonoEnableAsync(cancellationToken: cancellationToken);

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
            return debuggingServer;
        }

        public Task<string?> GetInitialBytesFromFile(string fileName, uint partition = 0, CancellationToken cancellationToken = default)
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
            string? serialPort = null;
            IMeadowDevice? meadow = null;

            if(_meadowDevice is MeadowSerialDevice device)
            {
                serialPort = device.SerialPort?.PortName;
            }

            _meadowDevice?.Dispose();

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            //try the old port first, if we still have it
            if (string.IsNullOrWhiteSpace(serialPort) == false)
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(serialPort!, false, Logger);
            }

            meadow ??= await MeadowDeviceManager.FindMeadowBySerialNumber(
                                                    serialNumber,
                                                    Logger,
                                                    cancellationToken: cancellationToken)
                                                .ConfigureAwait(false);
            

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
                    await MonoDisableAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    await Task.Delay(2000);

                    // Again, verify that Mono is disabled
                    Trace.Assert(await _meadowDevice.GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                                 "Meadow was expected to have Mono Disabled");

                    await _meadowDevice.UpdateMonoRuntimeAsync(
                        runtimePath,
                        cancellationToken: cancellationToken);

                    await Task.Delay(2000);

                    await ReInitializeMeadowAsync(cancellationToken)
                        .ConfigureAwait(false);

                    await Task.Delay(2000);
                }
                else
                {
                    Logger.LogInformation("Skipping update of runtime.");
                }

                if (skipEsp == false)
                {
                    await MonoDisableAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    Trace.Assert(await GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                                 "Meadow was expected to have Mono Disabled");

                    Logger.LogInformation("Updating ESP");
                    await _meadowDevice.FlashEspAsync(cancellationToken)
                                       .ConfigureAwait(false);

                    // Reset the meadow again to ensure flash worked.
                    await _meadowDevice.ResetMeadowAsync(cancellationToken)
                                       .ConfigureAwait(false);

                    await Task.Delay(3000);

                    await ReInitializeMeadowAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    Logger.LogInformation("Skipping ESP flash");
                }

                //Logger.LogInformation("Enabling Mono and Resetting");
                //await MonoEnableAsync(cancellationToken);

                // This is to ensure the ESP info has updated in HCOM on the Meadow
                await Task.Delay(3000, cancellationToken)
                          .ConfigureAwait(false);

                // TODO: Verify that the device info returns the expected version
                var deviceInfo = await _meadowDevice
                                       .GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken)
                                       .ConfigureAwait(false);

                Logger.LogInformation(
                    $"Updated Meadow to OS: {deviceInfo.MeadowOsVersion} ESP: {deviceInfo.CoProcessorOsVersion}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error flashing OS to Meadow");
            }
        }

        public static async Task<string> DfuFlashAsync(string serialPortName, string osPath, ILogger logger, CancellationToken cancellationToken = default)
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