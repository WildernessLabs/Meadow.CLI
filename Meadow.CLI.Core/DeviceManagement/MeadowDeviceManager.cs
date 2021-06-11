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
using Microsoft.Extensions.Logging.Abstractions;

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

        public static async Task<MeadowDevice?> GetMeadowForSerialPort(string serialPort, bool verbose = true, ILogger? logger = null)
        {
            logger ??= NullLogger.Instance;

            try
            {
                logger.LogInformation($"Connecting to Meadow on {serialPort}", serialPort);
                var meadow = new MeadowSerialDevice(serialPort, logger);

                await meadow.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

                return meadow;
            }
            catch (FileNotFoundException fnfEx)
            {
                
                LogUserError(verbose, logger);

                logger.LogDebug(fnfEx, "Failed to open Serial Port.");
                return null;
            }
            catch (IOException ioEx)
            {
                LogUserError(verbose, logger);

                logger.LogDebug(ioEx, "Failed to open Serial Port.");
                return null;
            }
            catch (UnauthorizedAccessException unAuthEx) when (
                unAuthEx.InnerException is IOException)
            {
                LogUserError(verbose, logger);

                logger.LogDebug(unAuthEx, "Failed to open Serial Port.");
                return null;
            }
            catch (Exception ex)
            {
                // TODO: Remove exception catch here and let the caller handle it or wrap it up in our own exception type.
                logger.LogError(ex, "Failed to connect to Meadow on {serialPort}", serialPort);
                throw;
            }
        }

        private static void LogUserError(bool verbose, ILogger logger)
        {
            if (verbose)
            {
                // TODO: Move message to ResourceManager or other tool for localization
                logger.LogError(
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
                IEnumerable<string>? ports;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ports = Directory.GetFiles("/dev", "tty.usb*");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ports = GetMeadowSerialPortsForOsx();
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
                        var device = await GetMeadowForSerialPort(port, false).ConfigureAwait(false);
                        if (device == null)
                            continue;

                        var deviceInfo =
                            await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);

                        if (deviceInfo!.SerialNumber == serialNumber)
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

        public static List<string> GetMeadowSerialPortsForOsx(ILogger? logger = null)
        {
            logger ??= NullLogger.Instance;
            logger.LogDebug("Get Meadow Serial ports");
            var ports = new List<string>();

            var psi = new ProcessStartInfo
                      {
                          FileName = "/usr/sbin/ioreg",
                          UseShellExecute = false,
                          RedirectStandardOutput = true,
                          Arguments = "-r -c IOUSBHostDevice -l"
                      };

            string output = string.Empty;

            using (var p = Process.Start(psi))
            {
                if (p != null)
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                }
            }

            //split into lines
            var lines = output.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            bool foundMeadow = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Meadow F7 Micro"))
                {
                    foundMeadow = true;
                }
                else if (lines[i].IndexOf("+-o", StringComparison.Ordinal) == 0)
                {
                    foundMeadow = false;
                }

                //now find the IODialinDevice entry which contains the serial port name
                if (foundMeadow && lines[i].Contains("IODialinDevice"))
                {
                    int startIndex = lines[i].IndexOf("/");
                    int endIndex = lines[i].IndexOf("\"", startIndex + 1);
                    var port = lines[i].Substring(startIndex, endIndex - startIndex);
                    logger.LogDebug($"Found Meadow at {port}", port);

                    ports.Add(port);
                    foundMeadow = false;
                }
            }
            logger.LogDebug("Found {count} ports", ports.Count);
            return ports;
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

        public async Task FlashOsAsync(string serialPortName, string osPath = "", string runtimePath = "", bool skipDfu = false, bool skipRuntime = false, bool skipEsp = false, CancellationToken cancellationToken = default)
        {
            var dfuAttempts = 0;
            
            string serialNumber;
            if (skipDfu)
            {
                _logger.LogInformation("Skipping DFU flash step.");
                using var device = await GetMeadowForSerialPort(serialPortName, false).ConfigureAwait(false);
                if (device == null)
                {
                    _logger.LogWarning("Cannot find Meadow on {port}", serialPortName);
                    return;
                }

                var deviceInfo = await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken)
                                             .ConfigureAwait(false);

                serialNumber = deviceInfo!.SerialNumber;
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
                        using var device = await GetMeadowForSerialPort(serialPortName, false).ConfigureAwait(false);

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
                await DfuUtils.FlashOsAsync(osPath, dfuDevice, _logger);
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

                if (skipRuntime == false)
                {
                    await device.UpdateMonoRuntimeAsync(
                        runtimePath,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Skipping update of runtime.");
                }

                // Again, verify that Mono is disabled
                Trace.Assert(await device.GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false,
                             "Meadow was expected to have Mono Disabled");

                if (skipEsp == false)
                {
                    _logger.LogInformation("Updating ESP");
                    await device.FlashEspAsync(cancellationToken)
                                .ConfigureAwait(false);

                    // Reset the meadow again to ensure flash worked.
                    await device.ResetMeadowAsync(cancellationToken)
                                .ConfigureAwait(false);
                }
                else
                {
                    _logger.LogInformation("Skipping ESP flash");
                }

                _logger.LogInformation("Enabling Mono and Resetting");
                while (await device.GetMonoRunStateAsync(cancellationToken).ConfigureAwait(false) == false)
                {
                    await device.MonoEnableAsync(cancellationToken);
                }

                // This is to ensure the ESP info has updated in HCOM on the Meadow
                await Task.Delay(2000, cancellationToken)
                          .ConfigureAwait(false);

                // TODO: Verify that the device info returns the expected version
                var deviceInfo = await device
                                             .GetDeviceInfoAsync(TimeSpan.FromSeconds(5), cancellationToken)
                                             .ConfigureAwait(false);

                _logger.LogInformation(
                    $"Updated Meadow to OS: {deviceInfo.MeadowOsVersion} ESP: {deviceInfo.CoProcessorOs}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flashing OS to Meadow");
            }
        }
    }
}