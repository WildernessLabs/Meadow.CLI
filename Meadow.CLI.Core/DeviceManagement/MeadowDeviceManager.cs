using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Exceptions;
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

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceManager
    {
        internal const int PreviousMaxAllowableMsgPacketLength = 512;
        static internal int MaxAllowableMsgPacketLength = 8192;

        static internal int MaxEstimatedSizeOfEncodedPayload = MaxAllowableMsgPacketLength + (MaxAllowableMsgPacketLength / 254) + 8;

        internal const int ProtocolHeaderSize = 12;

        static internal int MaxAllowableMsgPayloadLength = MaxAllowableMsgPacketLength - ProtocolHeaderSize;

        public const string NoDevicesFound = "No Devices Found";

        static object lockObject = new object();
        static IMeadowDevice? meadow = null;

        // Avoid changing signature
        public static async Task<IMeadowDevice?> GetMeadowForSerialPort(string serialPort, bool verbose = true, ILogger? logger = null)
        {
            logger ??= NullLogger.Instance;

            try
            {
                if (meadow != null)
                {
                    meadow.Dispose();
                    meadow = null;
                }

                logger.LogInformation($"{Environment.NewLine}Connecting to Meadow on {serialPort}{Environment.NewLine}");

                var createTask = Task.Run(() => meadow = new MeadowSerialDevice(serialPort, logger));
                var completedTask = await Task.WhenAny(createTask, Task.Delay(1000));

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

                await meadow.Initialize(CancellationToken.None);

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

        public async static Task<IList<string>> GetSerialPorts()
        {
            try
            {

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return await GetMeadowSerialPortsForLinux();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return await GetMeadowSerialPortsForOsx();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    lock (lockObject)
                    {
                        return GetMeadowSerialPortsForWindows();
                    }
                }
                else
                {
                    throw new Exception("Unknown operating system.");
                }
            }
            catch (Exception ex)
            {
                throw new DeviceNotFoundException($"Error Finding Meadow Devices on available Serial Ports: {ex.Message}");
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

                var ports = await GetSerialPorts();
                foreach (var port in ports)
                {
                    try
                    {
                        var device = await GetMeadowForSerialPort(port, false, logger);

                        if (device == null)
                            continue;

                        var deviceInfo = await device.GetDeviceInfo(
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

                await Task.Delay(1000, cancellationToken);

                attempts++;
            }

            throw new DeviceNotFoundException(
                $"Could not find a connected Meadow with the serial number {serialNumber}");
        }

        public async static Task<IList<string>> GetMeadowSerialPortsForOsx(ILogger? logger = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) == false)
                throw new PlatformNotSupportedException("This method is only supported on macOS");

            return await Task.Run(() =>
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
            });
        }

        public async static Task<IList<string>> GetMeadowSerialPortsForLinux(ILogger? logger = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == false)
                throw new PlatformNotSupportedException("This method is only supported on Linux");

            return await Task.Run(() =>
            {
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
                _ = proc?.WaitForExit(1000);
                var output = proc?.StandardOutput.ReadToEnd();

                return output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(x => x.Contains("Wilderness_Labs"))
                      .Select(
                          line =>
                          {
                              var parts = line.Split("-> ");
                              var target = parts[1];
                              var port = Path.GetFullPath(Path.Combine(devicePath, target));
                              return port;
                          }).ToArray();
            });
        }

        public static IList<string> GetMeadowSerialPortsForWindows(ILogger? logger = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
                throw new PlatformNotSupportedException("This method is only supported on Windows");

            logger ??= NullLogger.Instance;

            try
            {
                const string WildernessLabsPnpDeviceIDPrefix = @"USB\VID_" + Constants.WILDERNESS_LABS_USB_VID;

                // Win32_PnPEntity lives in root\CIMV2
                const string wmiScope = "root\\CIMV2";

                // escape special characters in the device id prefix
                string escapedPrefix = WildernessLabsPnpDeviceIDPrefix.Replace("\\", "\\\\").Replace("_", "[_]");

                // our query for all ports that have a PnP device id starting with Wilderness Labs' USB VID.
                string query = @$"SELECT Name, Caption, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Ports' AND PNPDeviceID like '{escapedPrefix}%'";

                logger.LogDebug("Running WMI Query: {query}", query);

                List<string> results = new();

                // build the searcher for the query
                using ManagementObjectSearcher searcher = new(wmiScope, query);

                // get the query results
                foreach (ManagementObject moResult in searcher.Get())
                {
                    // Try Caption and if not Name, they both seems to contain the COM port 
                    string portLongName = moResult["Caption"].ToString();
                    if (string.IsNullOrEmpty(portLongName))
                        portLongName = moResult["Name"].ToString();
                    string pnpDeviceId = moResult["PNPDeviceID"].ToString();

                    // we could collect and return a fair bit of other info from the query:

                    //string description = moResult["Description"].ToString();
                    //string service = moResult["Service"].ToString();
                    //string manufacturer = moResult["Manufacturer"].ToString();

                    var comIndex = portLongName.IndexOf("(COM") + 1;
                    var copyLength = portLongName.IndexOf(")") - comIndex;
                    var port = portLongName.Substring(comIndex, copyLength);

                    // the meadow serial is in the device id, after
                    // the characters: USB\VID_XXXX&PID_XXXX\
                    // so we'll just split is on \ and grab the 3rd element as the format is standard, but the length may vary.
                    var splits = pnpDeviceId.Split('\\');
                    var serialNumber = splits[2];

                    logger.LogDebug($"Found Wilderness Labs device at `{port}` with serial `{serialNumber}`");
                    results.Add($"{port}"); // removed serial number for consistency and will break fallback ({serialNumber})");
                }

                return results.ToArray();
            }
            catch (Exception aex)
            {
                // eat it for now
                logger.LogDebug(aex, "This error can be safely ignored.");

                // Since WMI Failed fall back to using SerialPort
                var ports = SerialPort.GetPortNames();

                //hack to skip COM1
                ports = ports.Where((source, index) => source != "COM1").Distinct().ToArray();

                return ports;
            }
        }
    }
}
