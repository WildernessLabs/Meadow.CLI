using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceManager
    {
        internal const int MaxAllowableMsgPacketLength = 512;

        internal const int MaxEstimatedSizeOfEncodedPayload =
            MaxAllowableMsgPacketLength + (MaxAllowableMsgPacketLength / 254) + 8;

        internal const int ProtocolHeaderSize = 12;

        internal const int MaxAllowableMsgPayloadLength =
            MaxAllowableMsgPacketLength - ProtocolHeaderSize;

        // Avoid changing signature
        public static async Task<IMeadowDevice?> GetMeadowForSerialPort(string serialPort, bool verbose = true, ILogger? logger = null)
        {
            logger ??= NullLogger.Instance;

            try
            {
                logger.LogInformation($"Connecting to Meadow on {serialPort}", serialPort);
                IMeadowDevice? meadow = null;
                var createTask = Task.Run(() => meadow = new MeadowSerialDevice(serialPort, logger));
                var completedTask = await Task.WhenAny(createTask, Task.Delay(1000))
                          .ConfigureAwait(false);

                if (completedTask != createTask || meadow == null)
                {
                    logger.LogTrace("Timeout while creating Meadow");
                    try
                    {
                        await createTask;
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation(ex, "An error occurred while attempting to create Meadow");
                        throw;
                    }
                    return null;
                }

                await meadow.InitializeAsync(CancellationToken.None);

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

        public static IList<string> GetSerialPorts()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetMeadowSerialPortsForLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMeadowSerialPortsForOsx();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetMeadowSerialPortsForWindows();
            }
            else
            {
                throw new Exception("Unknown operating system.");
            }
        }

        public static async Task<IMeadowDevice?> FindMeadowBySerialNumber(
            string serialNumber,
            ILogger logger,
            int maxAttempts = 10,
            CancellationToken cancellationToken = default)
        {
            var attempts = 0;
            while (attempts < maxAttempts)
            {

                var ports = GetSerialPorts();
                foreach (var port in ports)
                {
                    try
                    {
                        var device = await GetMeadowForSerialPort(port, false, logger)
                                         .ConfigureAwait(false);

                        if (device == null)
                            continue;

                        var deviceInfo = await device.GetDeviceInfoAsync(
                                             TimeSpan.FromSeconds(60),
                                             cancellationToken);

                        if (deviceInfo!.SerialNumber == serialNumber)
                        {
                            return device;
                        }

                        device.Dispose();
                    }
                    catch (UnauthorizedAccessException unauthorizedAccessException)
                    {
                        if (unauthorizedAccessException.InnerException is IOException)
                        {
                            // Eat it and retry
                            logger.LogDebug(
                                unauthorizedAccessException,
                                "This error can be safely ignored.");
                        }
                        else
                        {
                            logger.LogError(
                                unauthorizedAccessException,
                                "An unknown error has occurred while finding meadow");

                            throw;
                        }
                    }
                    catch (IOException ioException)
                    {
                        // Eat it and retry
                        logger.LogDebug(
                            ioException,
                            "This error can be safely ignored.");
                    }
                    catch (MeadowDeviceException meadowDeviceException)
                    {
                        // eat it for now
                        logger.LogDebug(
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

        public static IList<string> GetMeadowSerialPortsForOsx(ILogger? logger = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) == false)
                throw new PlatformNotSupportedException("This method is only supported on macOS");

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

            var foundMeadow = false;
            foreach (var line in lines)
            {
                if (line.Contains("Meadow F7 Micro"))
                {
                    foundMeadow = true;
                }
                else if (line.IndexOf("+-o", StringComparison.Ordinal) == 0)
                {
                    foundMeadow = false;
                }

                //now find the IODialinDevice entry which contains the serial port name
                if (foundMeadow && line.Contains("IODialinDevice"))
                {
                    int startIndex = line.IndexOf("/");
                    int endIndex = line.IndexOf("\"", startIndex + 1);
                    var port = line.Substring(startIndex, endIndex - startIndex);
                    logger.LogDebug($"Found Meadow at {port}", port);

                    ports.Add(port);
                    foundMeadow = false;
                }
            }
            logger.LogDebug("Found {count} ports", ports.Count);
            return ports;
        }

        public static IList<string> GetMeadowSerialPortsForLinux(ILogger? logger = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == false)
                throw new PlatformNotSupportedException("This method is only supported on Linux");

            const string devicePath = "/dev/serial/by-id";
            logger ??= NullLogger.Instance;
            var psi = new ProcessStartInfo()
                      {
                          FileName = "ls",
                          Arguments = $"-l {devicePath}",
                          UseShellExecute = false,
                          RedirectStandardOutput = true
                      };

            using var proc = Process.Start(psi);
            proc.WaitForExit(1000);
            var output = proc.StandardOutput.ReadToEnd();

            return output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                  .Where(x => x.Contains("Wilderness_Labs"))
                  .Select(
                      line =>
                      {
                          var parts = line.Split(' ');
                          var source = parts[8];
                          var target = parts[10];
                          var port = Path.GetFullPath(Path.Combine(devicePath, target));
                          return port;
                      }).ToArray();
        }

        public static IList<string> GetMeadowSerialPortsForWindows(ILogger? logger = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
            {
                throw new PlatformNotSupportedException("This method is only supported on Windows");
            }

            logger ??= NullLogger.Instance;

            try
            {
                const string WildernessLabsPnpDeviceIDPrefix = @"USB\VID_" + MeadowCLI.Constants.WILDERNESS_LABS_USB_VID;

                // Win32_PnPEntity lives in root\CIMV2
                const string wmiScope = "root\\CIMV2";

                // escape special characters in the device id prefix
                string escapedPrefix = WildernessLabsPnpDeviceIDPrefix.Replace("\\", "\\\\").Replace("_", "[_]");

                // our query for all ports that have a PnP device id starting with Wilderness Labs' USB VID.
                string query = @$"select * from Win32_PnPEntity where PNPClass = 'Ports' and PNPDeviceID like '{escapedPrefix}%'";

                logger.LogDebug("Running WMI Query: {query}", query);

                List<string> results = new();

                // build the searcher for the query
                using ManagementObjectSearcher searcher = new(wmiScope, query);

                // get the query results
                foreach (ManagementObject moResult in searcher.Get())
                {
                    string caption = moResult["Caption"].ToString();
                    string pnpDeviceId = moResult["PNPDeviceID"].ToString();

                    // we could collect and return a fair bit of other info from the query:

                    //string description = moResult["Description"].ToString();
                    //string service = moResult["Service"].ToString();
                    //string manufacturer = moResult["Manufacturer"].ToString();

                    // the meadow serial is in the device id, after
                    // the characters: USB\VID_XXXX&PID_XXXX\
                    // so we'll just take a substring after 22 characters.
                    const int serialNumberStartPosition = 22;
                    string serialNumber = pnpDeviceId.Substring(serialNumberStartPosition);

                    // unfortunately, the cleanest way to get the com port name from the Win32_PnPEntity class, is to extract
                    // it from the caption string. Win32_SerialPort places it directly in `DeviceID`, but some Windows installs
                    // don't always show all the ports. seealso: https://stackoverflow.com/questions/19840811
                    string portName = caption.Substring(caption.LastIndexOf("(COM")).Replace("(", string.Empty).Replace(")", string.Empty);

                    logger.LogDebug("Found Wilderness Labs device at `{portName}` with serial `{serialNumber}`", portName, serialNumber);
                    results.Add(portName);
                }

                return results;
            }
            catch (ApplicationException aex)
            {
                // eat it for now
                logger.LogDebug(
                    aex,
                    "This error can be safely ignored.");

                // we failed to find a port using wmi, fall back to
                // returning every potential port as reported by SerialPort.
                string[]? ports = SerialPort.GetPortNames();

                //hack to skip COM1
                ports = ports.Where((source, index) => source != "COM1").Distinct().ToArray();

                return ports;
            }
        }
    }
}