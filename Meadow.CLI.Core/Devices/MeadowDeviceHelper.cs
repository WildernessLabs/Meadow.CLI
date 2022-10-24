using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
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

        public Task<IDictionary<string, uint>> GetFilesAndCrcs(TimeSpan timeout, int partition = 0, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetFilesAndCrcs(timeout, partition, cancellationToken);
        }

        public Task<IList<string>> GetFilesAndFolders(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetFilesAndFolders(timeout, cancellationToken);
        }

        public Task<FileTransferResult> WriteFile(string sourceFileName, string destinationFileName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.WriteFile(sourceFileName, destinationFileName, timeout, cancellationToken);
        }

        public Task DeleteFile(string fileName,
                                    uint partition = 0,
                                    CancellationToken cancellationToken = default)
        {
            return _meadowDevice.DeleteFile(fileName, partition, cancellationToken);
        }

        public Task EraseFlash(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.EraseFlash(cancellationToken);
        }

        public Task VerifyErasedFlash(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.VerifyErasedFlash(cancellationToken);
        }

        public Task FormatFileSystem(uint partition = 0, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.FormatFileSystem(partition, cancellationToken);
        }

        public Task RenewFileSystem(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.RenewFileSystem(cancellationToken);
        }

        public async Task UpdateMonoRuntime(string? fileName = null, string? osVersion = null, uint partition = 0, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting Mono Runtime Update");
            Logger.LogDebug("Calling Mono Disable");
            await MonoDisable(cancellationToken: cancellationToken);

            await ReInitializeMeadow(cancellationToken);

            Trace.Assert(await GetMonoRunState(cancellationToken) == false,
                         "Meadow was expected to have Mono Disabled");

            Logger.LogInformation("Updating Mono Runtime");

            await _meadowDevice.UpdateMonoRuntime(fileName, osVersion, partition, cancellationToken);
        }

        public Task WriteFileToEspFlash(string fileName, uint partition = 0, string? mcuDestAddress = null, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.WriteFileToEspFlash(
                fileName,
                partition,
                mcuDestAddress,
                cancellationToken);
        }

        public Task FlashEsp(string? sourcePath = null, string? osVersion = null, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.FlashEsp(sourcePath, osVersion, cancellationToken);
        }

        //Get's the OS version as a string, used by the download manager
        public async Task<string> GetOSVersion(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var deviceInfo = await GetDeviceInfo(timeout, cancellationToken);
            return deviceInfo.MeadowOsVersion.Split(' ')[0]; // we want the first part of e.g. '0.5.3.0 (Oct 13 2021 13:39:12)'
        }

        public Task<MeadowDeviceInfo> GetDeviceInfo(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceInfo(timeout, cancellationToken);
        }

        public Task<string?> GetDeviceName(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceName(timeout, cancellationToken);
        }

        public Task<bool> GetMonoRunState(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetMonoRunState(cancellationToken);
        }

        public async Task MonoDisable(bool force = false, CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState = await GetMonoRunState(cancellationToken);
            while ((monoRunState == true || force)
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Disable Request (Forced? {forced})", force);
                await _meadowDevice.MonoDisable(cancellationToken);

                Logger.LogDebug("Waiting for Meadow to restart");
                await Task.Delay(3000, cancellationToken);

                Logger.LogDebug("Reinitialize the device");
                await ReInitializeMeadow(cancellationToken);
                force = false;

                Logger.LogDebug("Check Mono state");
                monoRunState = await GetMonoRunState(cancellationToken);
            }

            if (monoRunState)
            {
                throw new Exception("Failed to stop mono.");
            }
        }

        public async Task MonoEnable(bool force = false, CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(60));
            bool monoRunState = await GetMonoRunState(cancellationToken);
            while ((monoRunState == false || force)
                && endTime > DateTime.UtcNow)
            {
                Logger.LogDebug("Sending Mono Enable Request (Forced? {forced})", force);
                await _meadowDevice.MonoEnable(cancellationToken);

                Logger.LogDebug("Waiting for Meadow to restart");
                await Task.Delay(1000, cancellationToken);

                Logger.LogDebug("Reinitialize the device");
                await ReInitializeMeadow(cancellationToken);
                force = false;

                Logger.LogDebug("Check Mono state");
                monoRunState = await GetMonoRunState(cancellationToken);
            }

            if (!monoRunState)
                throw new Exception("Failed to enable mono.");
        }

        public async Task ResetMeadow(CancellationToken cancellationToken = default)
        {
            await _meadowDevice.ResetMeadow(cancellationToken);
            await Task.Delay(1000, cancellationToken);
            await ReInitializeMeadow(cancellationToken);
        }

        public Task MonoFlash(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.MonoFlash(cancellationToken);
        }

        public Task EnterDfuMode(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.EnterDfuMode(cancellationToken);
        }

        public Task NshEnable(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.NshEnable(cancellationToken);
        }

        public Task NshDisable(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.NshDisable(cancellationToken);
        }

        public Task TraceEnable(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.TraceEnable(cancellationToken);
        }

        public Task SetTraceLevel(uint traceLevel, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.SetTraceLevel(traceLevel, cancellationToken);
        }

        public Task TraceDisable(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.TraceDisable(cancellationToken);
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

        public Task QspiWrite(int value, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.QspiWrite(value, cancellationToken);
        }

        public Task QspiRead(int value, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.QspiRead(value, cancellationToken);
        }

        public Task QspiInit(int value, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.QspiInit(value, cancellationToken);
        }

        public async Task DeployApp(string fileName, bool includePdbs = true, CancellationToken cancellationToken = default)
        {
            try
            {
                await MonoDisable(cancellationToken: cancellationToken);

                string osVersion = await GetOSVersion(TimeSpan.FromSeconds(30), cancellationToken);

                await _meadowDevice.DeployApp(fileName, osVersion, includePdbs, cancellationToken);

                await MonoEnable(true, cancellationToken: cancellationToken);

                await Task.Delay(2000, cancellationToken);
            }
            catch (Exception ex) {
                await MonoDisable(true, cancellationToken);
                throw ex;
            }
        }

        public Task ForwardVisualStudioDataToMono(byte[] debuggerData, uint userData, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.ForwardVisualStudioDataToMono(
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
        public async Task<DebuggingServer> StartDebuggingSession(int port, CancellationToken cancellationToken)
        {
            Logger.LogDebug($"StartDebugging on port: {port}");
            await _meadowDevice.StartDebugging(port, cancellationToken);

            //Logger.LogDebug("Waiting for Meadow to restart");
            //await Task.Delay(1000, cancellationToken);

            Logger.LogDebug("Reinitialize the device");
            await ReInitializeMeadow(cancellationToken);

            if (_meadowDevice == null)
                throw new DeviceNotFoundException();

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var debuggingServer = new DebuggingServer(_meadowDevice, endpoint, Logger);

            Logger.LogDebug("Tell the Debugging Server to Start Listening");
            await debuggingServer.StartListening(cancellationToken);
            return debuggingServer;
        }

        public Task<string?> GetInitialBytesFromFile(string fileName, uint partition = 0, CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetInitialBytesFromFile(fileName, partition, cancellationToken);
        }

        public Task RestartEsp32(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.RestartEsp32(cancellationToken);
        }

        public Task<string?> GetDeviceMacAddress(CancellationToken cancellationToken = default)
        {
            return _meadowDevice.GetDeviceMacAddress(cancellationToken);
        }

        public bool IsDeviceInitialized()
        {
            return _meadowDevice.IsDeviceInitialized();
        }

        public async Task ReInitializeMeadow(CancellationToken cancellationToken = default)
        {
            var serialNumber = DeviceInfo.SerialNumber;
            string? serialPort = null;
            IMeadowDevice? meadow = null;

            if(_meadowDevice is MeadowSerialDevice device)
            {
                serialPort = device.SerialPort?.PortName;
            }

            _meadowDevice?.Dispose();

            await Task.Delay(1000, cancellationToken);

            //try the old port first, if we still have it
            if (string.IsNullOrWhiteSpace(serialPort) == false)
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(serialPort!, false, Logger);
            }

            meadow ??= await MeadowDeviceManager.FindMeadowBySerialNumber(
                                                    serialNumber,
                                                    Logger,
                                                    cancellationToken: cancellationToken);


            await Task.Delay(1000, cancellationToken);

            _meadowDevice = meadow ?? throw new Exception($"Meadow not found. Serial Number {serialNumber}");
        }

        public async Task WriteRuntimeAndEspBins(string? runtimePath = null, string? osVersion = null, bool skipRuntime = false, bool skipEsp = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (skipRuntime == false)
                {
                    await MonoDisable(cancellationToken: cancellationToken);

                    await Task.Delay(2000);

                    await _meadowDevice.UpdateMonoRuntime(
                        runtimePath, 
                        osVersion,
                        cancellationToken: cancellationToken);

                    await Task.Delay(2000);

                    await ReInitializeMeadow(cancellationToken);

                    await Task.Delay(2000);
                }
                else
                {
                    Logger.LogInformation("Skipping update of runtime.");
                }

                if (skipEsp == false)
                {
                    await MonoDisable(cancellationToken: cancellationToken);

                    //Trace.Assert(await GetMonoRunState(cancellationToken) == false,
                    //             "Meadow was expected to have Mono Disabled");

                    Logger.LogInformation("Updating ESP");

                    await _meadowDevice.FlashEsp(DownloadManager.FirmwareDownloadsFilePath, osVersion, cancellationToken);

                    // Reset the meadow again to ensure flash worked.
                    await _meadowDevice.ResetMeadow(cancellationToken);

                    await Task.Delay(3000);

                    await ReInitializeMeadow(cancellationToken);
                }
                else
                {
                    Logger.LogInformation("Skipping ESP flash");
                }

                //Logger.LogInformation("Enabling Mono and Resetting");
                //await MonoEnable(cancellationToken);

                // This is to ensure the ESP info has updated in HCOM on the Meadow
                await Task.Delay(3000, cancellationToken);

                // TODO: Verify that the device info returns the expected version
                var deviceInfo = await _meadowDevice
                                       .GetDeviceInfo(TimeSpan.FromSeconds(60), cancellationToken);

                Logger.LogInformation($"Updated Meadow to OS: {deviceInfo.MeadowOsVersion}, " +
                                    $"Mono: {deviceInfo.MonoVersion}, " +
                                    $"Coprocessor: {deviceInfo.CoProcessorOsVersion}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error flashing OS to Meadow");
            }
        }

        public static async Task<string> DfuFlash(string serialPortName, 
            string osPath,
            string? osVersion,
            ILogger logger, 
            CancellationToken cancellationToken = default)
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
                    {   // This is bad, we can't just blindly flash with multiple devices, let the user know
                        throw;
                    }
                    catch (DeviceNotFoundException)
                    {   // eat it.
                    }

                    // No DFU device found, lets try to set the meadow to DFU mode.
                    using var device = await MeadowDeviceManager.GetMeadowForSerialPort(serialPortName, false);

                    if (device != null)
                    {
                        logger.LogInformation("Entering DFU Mode");
                        await device.EnterDfuMode(cancellationToken);
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
                await Task.Delay(1000, cancellationToken);

                dfuAttempts++;
            }

            // Get the serial number so that later we can pick the right device if the system has multiple meadow plugged in
            string serialNumber = DfuUtils.GetDeviceSerial(dfuDevice);

            logger.LogInformation("Device in DFU Mode, flashing OS");
            var res = await DfuUtils.DfuFlash(osPath, osVersion ?? "", dfuDevice, logger);
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
            lock (Logger)
            {
                if (disposing)
                {
                    _meadowDevice?.Dispose();
                }
            }
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

        private Dictionary<string, string> deviceVersionTable = new ()
        {
            { "F7v1", "F7FeatherV1" },
            { "F7v2", "F7FeatherV2" },
        };

        public bool DeviceAndAppVersionsMatch(string executablePath, CancellationToken cancellationToken = default)
        {
            var deviceVersion = DeviceInfo.HardwareVersion;
            var assembly = Assembly.LoadFrom(executablePath);
            try 
            {
                var baseType = assembly.GetTypes()[0].BaseType.ToString();
                string appVersion = string.Empty;

                // IIRC using Linq would be way slower.
                foreach (var item in deviceVersionTable)
                {
                    if (baseType.Contains(item.Value))
                    {
                        appVersion = item.Value;
                        break;
                    }
                }

                if (deviceVersionTable[deviceVersion] != appVersion)
                {
                    Logger.LogInformation($"Current device version is: {deviceVersion}. Current application version is {appVersion}. Update your application version to {deviceVersionTable[deviceVersion]} to deploy to this Meadow device.");
                    return false;
                }
            }
            finally 
            {
                assembly = null;
            }

            return true;
        }
    }
}