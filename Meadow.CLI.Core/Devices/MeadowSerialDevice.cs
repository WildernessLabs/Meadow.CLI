using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.CLI.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.Devices
{
    public class MeadowSerialDevice : MeadowLocalDevice
    {
        private readonly string _serialPortName;
        public SerialPort? SerialPort { get; private set; }

        public MeadowSerialDevice(string serialPortName, IMeadowLogger? logger = null)
            : this(serialPortName, OpenSerialPort(serialPortName), logger)
        {
        }

        private MeadowSerialDevice(string serialPortName,
                                   SerialPort serialPort,
                                   IMeadowLogger? logger = null)
            : base(new MeadowSerialDataProcessor(serialPort, logger), logger)
        {
            SerialPort = serialPort;
            _serialPortName = serialPortName;
        }

        public override bool IsDeviceInitialized()
        {
            return SerialPort != null;
        }

        private protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Logger.LogTrace("Disposing SerialPort");
                SerialPort?.Dispose();
            }
        }

        public override async Task WriteAsync(byte[] encodedBytes, int encodedToSend, CancellationToken cancellationToken = default)
        {
            if (SerialPort == null || SerialPort.IsOpen == false)
            {
                Logger.LogDebug("SerialPort is null or not open.");
                throw new DeviceDisconnectedException();
            }

            await SerialPort.BaseStream.WriteAsync(encodedBytes, 0, encodedToSend, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<bool> InitializeAsync(CancellationToken cancellationToken)
        {
            var initTimeout = TimeSpan.FromSeconds(60);
            var now = DateTime.UtcNow;
            var then = now.Add(initTimeout);
            while (DateTime.UtcNow < then)
            {
                try
                {
                    if (SerialPort is { IsOpen: true })
                    {
                        Logger.LogDebug("Initializing Meadow for the first time");

                        // TODO: Find a way to flush all the garbage startup messages
                        await Task.Delay(1000, cancellationToken)
                                  .ConfigureAwait(false);

                        DeviceInfo = await GetDeviceInfoAsync(TimeSpan.FromSeconds(5), cancellationToken);

                        return true;
                    }
                }
                catch (MeadowCommandException meadowCommandException)
                {
                    Logger.LogTrace(
                        meadowCommandException,
                        "Caught exception while waiting for device to be ready. Retrying.");
                }
                catch (DeviceDisconnectedException ddEx)
                {
                    // eat it
                    Logger.LogTrace(ddEx,
                                    "Caught exception while waiting for device to be ready. Retrying.");
                }
                catch (Exception ex)
                {
                    Logger.LogTrace(ex, "Caught exception while waiting for device to be ready. Retrying.");
                }

                await Task.Delay(100, cancellationToken)
                          .ConfigureAwait(false);
            }

            throw new Exception($"Device not ready after {initTimeout}s");
        }

        private static SerialPort OpenSerialPort(string portName)
        {
            // Create a new SerialPort object with default settings
            var port = new SerialPort
                       {
                           PortName = portName,
                           BaudRate = 115200, // This value is ignored when using ACM
                           Parity = Parity.None,
                           DataBits = 8,
                           StopBits = StopBits.One,
                           Handshake = Handshake.None,

                           // Set the read/write timeouts
                           ReadTimeout = 5000,
                           WriteTimeout = 5000
                       };
            
            if (port.IsOpen)
                port.Close();
            port.Open();
            port.BaseStream.ReadTimeout = 0;

            return port;
        }
    }
}