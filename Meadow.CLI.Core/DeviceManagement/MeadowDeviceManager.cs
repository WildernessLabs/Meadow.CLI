using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibUsbDotNet.Main;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.Dfu;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        internal const int MaxAllowableMsgPacketLength = 512;

        internal const int MaxEstimatedSizeOfEncodedPayload =
            MaxAllowableMsgPacketLength + (MaxAllowableMsgPacketLength / 254) + 8;

        internal const int ProtocolHeaderSize = 12;

        internal const int MaxAllowableMsgPayloadLength =
            MaxAllowableMsgPacketLength - ProtocolHeaderSize;

        public MeadowDeviceManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<MeadowDeviceManager>();
        }

        public MeadowDevice? GetMeadowForSerialPort(string serialPort, bool verbose = true)
        {
            try
            {
                _logger.LogInformation($"Connecting to Meadow on {serialPort}", serialPort);
                var meadow = new MeadowSerialDevice(serialPort, _logger);

                meadow.Initialize();

                return meadow;
            }
            catch (FileNotFoundException fnfEx)
            {
                
                LogUserError(verbose);

                _logger.LogDebug(fnfEx, "Failed to open Serial Port.");
                return null;
            }
            catch (IOException ioEx)
            {
                LogUserError(verbose);

                _logger.LogDebug(ioEx, "Failed to open Serial Port.");
                return null;
            }
            catch (UnauthorizedAccessException unAuthEx) when (
                unAuthEx.InnerException is IOException)
            {
                LogUserError(verbose);

                _logger.LogDebug(unAuthEx, "Failed to open Serial Port.");
                return null;
            }
            catch (Exception ex)
            {
                // TODO: Remove exception catch here and let the caller handle it or wrap it up in our own exception type.
                _logger.LogError(ex, "Failed to connect to Meadow on {serialPort}", serialPort);
                throw;
            }
        }

        private void LogUserError(bool verbose)
        {
            if (verbose)
            {
                // TODO: Move message to ResourceManager or other tool for localization
                _logger.LogError(
                    "Failed to open Serial Port. Please ensure you have exclusive access to the serial port and the specified port exists.");
            }
        }

        public async Task<MeadowDevice?> FindMeadowBySerialNumber(
            string serialNumber,
            int maxAttempts = 10,
            CancellationToken cancellationToken = default)
        {
            var attempts = 0;
            while (attempts < maxAttempts)
            {
                string[]? ports;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ports = Directory.GetFiles("/dev", "tty.usb*");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ports = Directory.GetFiles("/dev", "tty.usb*");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ports = SerialPort.GetPortNames();
                }
                else
                {
                    throw new Exception("Unknown operating system.");
                }

                foreach (var port in ports)
                {
                    try
                    {
                        var device = GetMeadowForSerialPort(port, false);
                        if (device == null)
                            continue;

                        var deviceInfo =
                            await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);

                        if (!string.IsNullOrWhiteSpace(deviceInfo)
                         && deviceInfo!.Contains(serialNumber))
                        {
                            return device;
                        }

                        device.Dispose();
                    }
                    catch (MeadowDeviceException meadowDeviceException)
                    {
                        // eat it for now
                        _logger.LogDebug(
                            meadowDeviceException,
                            "This error can be safely ignored.");
                    }
                }

                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                attempts++;
            }

            throw new DeviceNotFoundException(
                $"Could not find a connected Meadow with the serial number {serialNumber}");
        }

        //we'll move this soon
        public static List<string> FindSerialDevices()
        {
            var devices = new List<string>();

            foreach (var s in SerialPort.GetPortNames())
            {
                //limit Mac searches to tty.usb*, Windows, try all COM ports
                //on Mac it's pretty quick to test em all so we could remove this check 
                if (Environment.OSVersion.Platform != PlatformID.Unix || s.Contains("tty.usb"))
                {
                    devices.Add(s);
                }
            }

            return devices;
        }

        public static List<string> GetSerialDeviceCaptions()
        {
            var devices = new List<string>();

            using (var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                foreach (var item in searcher.Get())
                {
                    devices.Add(
                        item["Caption"]
                            .ToString());
                }
            }

            return devices;
        }

        public async Task FlashOsAsync(string serialPortName, string binPath, bool skipDfu = false, CancellationToken cancellationToken = default)
        {
            var dfuAttempts = 0;
            
            string serialNumber;
            if (skipDfu)
            {
                _logger.LogInformation("Skipping DFU flash step.");
                using var device = GetMeadowForSerialPort(serialPortName, false);
                if (device == null)
                {
                    _logger.LogWarning("Cannot find Meadow on {port}", serialPortName);
                    return;
                }

                var deviceInfo = await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken)
                                             .ConfigureAwait(false);

                var f = new MeadowDeviceInfo(deviceInfo);
                serialNumber = f.SerialNumber;
            }
            else
            {
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
                        using var device = GetMeadowForSerialPort(serialPortName, false);

                        if (device != null)
                        {
                            _logger.LogInformation("Entering DFU Mode");
                            await device.EnterDfuModeAsync(cancellationToken)
                                        .ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(
                            "An exception occurred while switching device to DFU Mode. Exception: {0}",
                            ex);
                    }

                    switch (dfuAttempts)
                    {
                        case 5:
                            _logger.LogInformation(
                                "Having trouble putting Meadow in DFU Mode, please press RST button on Meadow and press enter to try again");

                            Console.ReadKey();
                            break;
                        case 10:
                            _logger.LogInformation(
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
                serialNumber = DfuUtils.GetDeviceSerial(dfuDevice);

                _logger.LogInformation("Device in DFU Mode, flashing OS");
                await DfuUtils.FlashOsAsync(device: dfuDevice, logger: _logger);
                _logger.LogInformation("Device Flashed.");
            }

            try
            {
                using var device = await FindMeadowBySerialNumber(
                                           serialNumber,
                                           cancellationToken: cancellationToken)
                                       .ConfigureAwait(false);

                if (device == null)
                {
                    _logger.LogWarning("Unable to find Meadow after DFU Flash.");
                    return;
                }

                await device.UpdateMonoRuntimeAsync(binPath, cancellationToken: cancellationToken);

                // Again, verify that Mono is disabled
                Trace.Assert(await device.GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                             "Meadow was expected to have Mono Disabled");

                _logger.LogInformation("Updating ESP");
                await device.FlashEspAsync(cancellationToken)
                            .ConfigureAwait(false);

                // Reset the meadow again to ensure flash worked.
                await device.ResetMeadowAsync(cancellationToken)
                            .ConfigureAwait(false);

                _logger.LogInformation("Enabling Mono and Resetting");
                while (await device.GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false)
                {
                    await device.MonoEnableAsync(cancellationToken);
                }

                await Task.Delay(2000, cancellationToken)
                          .ConfigureAwait(false);

                // TODO: Verify that the device info returns the expected version
                var deviceInfoString = await device
                                             .GetDeviceInfoAsync(TimeSpan.FromSeconds(5), cancellationToken)
                                             .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(deviceInfoString))
                {
                    throw new Exception("Unable to retrieve device info.");
                }

                var deviceInfo = new MeadowDeviceInfo(deviceInfoString);
                _logger.LogInformation(
                    $"Updated Meadow to OS: {deviceInfo.MeadowOSVersion} ESP: {deviceInfo.CoProcessorOs}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flashing OS to Meadow");
            }
        }
    }
}