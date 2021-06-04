using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

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
        internal const int MaxEstimatedSizeOfEncodedPayload = MaxAllowableMsgPacketLength + (MaxAllowableMsgPacketLength / 254) + 8;
        internal const int ProtocolHeaderSize = 12;
        internal const int MaxAllowableMsgPayloadLength = MaxAllowableMsgPacketLength - ProtocolHeaderSize;

        public MeadowDeviceManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<MeadowDeviceManager>();
        }

        public Task<MeadowDevice> GetMeadowForSerialPort(string serialPort, CancellationToken cancellationToken = default)//, bool verbose = true)
        {
            return GetMeadowForSerialPort(
                serialPort,
                _loggerFactory.CreateLogger<MeadowSerialDevice>(),
                cancellationToken);
        }

        public async Task<MeadowDevice> GetMeadowForSerialPort(string serialPort, ILogger<MeadowSerialDevice>? logger = null, CancellationToken cancellationToken = default)//, bool verbose = true)
        {
            try
            {
                _logger.LogInformation($"Connecting to Meadow on {serialPort}", serialPort);
                var meadow = new MeadowSerialDevice(serialPort, logger ?? new NullLogger<MeadowSerialDevice>());
                await meadow.InitializeAsync(cancellationToken).ConfigureAwait(false);
                return meadow;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Meadow on {serialPort}", serialPort);
                throw ex;
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
                        var device = await GetMeadowForSerialPort(port, cancellationToken);

                        var deviceInfo =
                            await device.GetDeviceInfoAsync(cancellationToken: cancellationToken);

                        if (!string.IsNullOrWhiteSpace(deviceInfo)
                         && deviceInfo!.Contains(serialNumber))
                        {
                            return device;
                        }

                        device.Dispose();
                    }
                    catch (FileNotFoundException fileNotFoundException)
                    {
                        // eat it for now
                        _logger.LogDebug(
                            fileNotFoundException,
                            "This error can be safely ignored.");
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
                if (Environment.OSVersion.Platform != PlatformID.Unix ||
                    s.Contains("tty.usb"))
                {
                    devices.Add(s);
                }
            }
            return devices;
        }

        public static List<string> GetSerialDeviceCaptions()
        {
            var devices = new List<string>();

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'"))
            {
                var portnames = SerialPort.GetPortNames();
                foreach (var item in searcher.Get())
                {
                    devices.Add(item["Caption"].ToString());
                }
            }
            return devices;
        }
    }
}
