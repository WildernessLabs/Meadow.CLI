using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    public class MeadowDeviceManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        internal const int MaxAllowableDataBlock = 512;
        internal const int MaxSizeOfPacketBuffer = MaxAllowableDataBlock + (MaxAllowableDataBlock / 254) + 8;
        internal const int ProtocolHeaderSize = 12;
        internal const int MaxDataSizeInProtocolMsg = MaxAllowableDataBlock - ProtocolHeaderSize;

        public MeadowDeviceManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<MeadowDeviceManager>();
        }

        public Task<MeadowDevice> GetMeadowForSerialPort(string serialPort, bool silent = false, CancellationToken cancellationToken = default)//, bool verbose = true)
        {
            return GetMeadowForSerialPort(
                serialPort,
                silent,
                _loggerFactory.CreateLogger<MeadowSerialDevice>(),
                cancellationToken);
        }

        public async Task<MeadowDevice> GetMeadowForSerialPort(string serialPort, bool silent = false, ILogger<MeadowSerialDevice>? logger = null, CancellationToken cancellationToken = default)//, bool verbose = true)
        {
            try
            {
                var meadow = new MeadowSerialDevice(serialPort, logger ?? new NullLogger<MeadowSerialDevice>());
                await meadow.Initialize(cancellationToken).ConfigureAwait(false);
                return meadow;

            }
            catch (Exception ex)
            {
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
                var ports = SerialPort.GetPortNames();
                foreach (var port in ports)
                {
                    try
                    {
                        var device = await GetMeadowForSerialPort(
                                         port,
                                         true,
                                         cancellationToken);

                        var deviceInfo =
                            await device.GetDeviceInfo(cancellationToken: cancellationToken);

                        if (!string.IsNullOrWhiteSpace(deviceInfo) && deviceInfo!.Contains(serialNumber))
                        {
                            return device;
                        }

                        device.Dispose();
                    }
                    catch (MeadowDeviceException meadowDeviceException)
                    {
                        // eat it for now
                        _logger.LogTrace(
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
