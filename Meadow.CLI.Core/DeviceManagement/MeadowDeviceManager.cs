using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
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

        public static async Task<IMeadowDevice?> GetMeadowForSerialPort(string serialPort, bool verbose = true, ILogger? logger = null)
        {
            logger ??= NullLogger.Instance;

            try
            {
                logger.LogInformation($"Connecting to Meadow on {serialPort}", serialPort);
                IMeadowDevice? meadow = null;
                var createTask= Task.Run(() => meadow = new MeadowSerialDevice(serialPort, logger));
                var delayTask = Task.Delay(1000);
                var completedTask = await Task.WhenAny(createTask, delayTask)
                          .ConfigureAwait(false);

                if (completedTask == delayTask || meadow == null)
                {
                    logger.LogTrace("Timeout while creating meadow.");
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

        public static async Task<IMeadowDevice?> FindMeadowBySerialNumber(
            string serialNumber,
            ILogger logger,
            int maxAttempts = 10,
            CancellationToken cancellationToken = default)
        {
            var attempts = 0;
            while (attempts < maxAttempts)
            {
                IEnumerable<string>? ports;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ports = GetMeadowSerialPortsForOsx();
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
                        var device = await GetMeadowForSerialPort(port, false, logger).ConfigureAwait(false);
                        if (device == null)
                            continue;

                        var deviceInfo =
                            await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken);

                        if (deviceInfo!.SerialNumber == serialNumber)
                        {
                            return device;
                        }

                        device.Dispose();
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
    }
}