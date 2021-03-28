using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    public class MeadowDeviceManager
    {
        //private static Dictionary<string, MeadowSerialDevice> _connections = new Dictionary<string, MeadowSerialDevice>();
        internal const int MaxAllowableDataBlock = 512;
        internal const int MaxSizeOfPacketBuffer = MaxAllowableDataBlock + (MaxAllowableDataBlock / 254) + 8;
        internal const int ProtocolHeaderSize = 12;
        internal const int MaxDataSizeInProtocolMsg = MaxAllowableDataBlock - ProtocolHeaderSize;

        public static async Task<MeadowLocalDevice> GetMeadowForSerialPort(string serialPort, bool silent = false, ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default)//, bool verbose = true)
        {
            try
            {
                loggerFactory ??= new NullLoggerFactory();
                //if (_connections.ContainsKey(serialPort))
                //{
                //    _connections[serialPort].Dispose();
                //    _connections.Remove(serialPort);
                //    await Task.Delay(1000, cancellationToken)
                //              .ConfigureAwait(false);
                //}

                var meadow = new MeadowSerialDevice(serialPort, loggerFactory.CreateLogger<MeadowSerialDevice>());
                await meadow.Initialize(cancellationToken).ConfigureAwait(false);
                //_connections.Add(serialPort, meadow);
                return meadow;

            }
            catch (Exception ex)
            {
                throw ex;
            }
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
