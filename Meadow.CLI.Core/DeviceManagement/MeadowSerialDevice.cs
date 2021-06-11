using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public partial class MeadowSerialDevice : MeadowLocalDevice
    {
        private readonly string _serialPortName;
        public SerialPort? SerialPort { get; private set; }

        public MeadowSerialDevice(string serialPortName, ILogger? logger = null)
            : this(serialPortName, OpenSerialPort(serialPortName), logger)
        {
        }

        private MeadowSerialDevice(string serialPortName,
                                   SerialPort serialPort,
                                   ILogger? logger = null)
            : base(new MeadowSerialDataProcessor(serialPort, logger), logger)
        {
            SerialPort = serialPort;
            _serialPortName = serialPortName;
        }

        public sealed override bool IsDeviceInitialized()
        {
            return SerialPort != null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Logger.LogTrace("Disposing SerialPort");
                SerialPort?.Dispose();
            }
        }

        public override async Task WriteAsync(byte[] encodedBytes, int encodedToSend, CancellationToken cancellationToken = default)
        {
            if (SerialPort == null)
                throw new NotConnectedException();

            if (SerialPort.IsOpen == false)
            {
                Logger.LogDebug("Port is not open, attempting reconnect.");
                await ReInitializeAsync(cancellationToken);
            }

            await SerialPort.BaseStream.WriteAsync(encodedBytes, 0, encodedToSend, cancellationToken).ConfigureAwait(false);
        }

        public override async Task<bool> ReInitializeAsync(CancellationToken cancellationToken)
        {
            var serialNumber = DeviceInfo.SerialNumber;
            var now = DateTime.UtcNow;
            var then = now.Add(TimeSpan.FromSeconds(300));
            while (DateTime.UtcNow < then)
            {
                try
                {
                    DataProcessor?.Dispose();
                    DataProcessor = null;
                    SerialPort?.Dispose();
                    SerialPort = null;
                    var attempts = 0;
                    while (attempts < 10)
                    {
                        var ports = SerialPort.GetPortNames();
                        foreach (var port in ports)
                        {
                            Logger.LogTrace("Trying serial port {port}", port);
                            SerialPort = OpenSerialPort(port);
                            Logger.LogTrace("Creating new DataProcessor");
                            DataProcessor = new MeadowSerialDataProcessor(SerialPort, Logger);
                            Logger.LogTrace("Waiting for DataProcessor to start");
                            await Task.Delay(1000, cancellationToken)
                                      .ConfigureAwait(false);
                            Logger.LogTrace("Requesting Device Information");
                            var deviceInfo = await GetDeviceInfoAsync(
                                                 DefaultTimeout,
                                                 cancellationToken);

                            if (deviceInfo?.SerialNumber == serialNumber)
                            {
                                Logger.LogTrace("Device serial number match!");
                                DeviceInfo = deviceInfo;
                                return true;
                            }

                            SerialPort.Dispose();
                            DataProcessor.Dispose();
                        }

                        attempts++;
                    }

                    if (DeviceInfo != null)
                    {
                        return true;
                    }
                }
                catch (MeadowCommandException meadowCommandException)
                {
                    Logger.LogTrace(meadowCommandException, "Caught exception while waiting for device to be ready");
                }
                catch (Exception ex)
                {
                    Logger.LogTrace(ex, "Caught exception while waiting for device to be ready. Retrying.");
                }

                await Task.Delay(100, cancellationToken)
                          .ConfigureAwait(false);
            }

            throw new Exception($"Device not ready after {300}ms");
        }

        public override async Task<bool> InitializeAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var then = now.Add(TimeSpan.FromSeconds(300));
            while (DateTime.UtcNow < then)
            {
                try
                {
                    if (SerialPort is { IsOpen: true })
                    {
                        Logger.LogDebug("Initializing Meadow for the first time");
                        DeviceInfo = await GetDeviceInfoAsync(DefaultTimeout, cancellationToken);
                        return true;
                    }
                }
                catch (MeadowCommandException meadowCommandException)
                {
                    Logger.LogTrace(meadowCommandException, "Caught exception while waiting for device to be ready");
                }
                catch (Exception ex)
                {
                    Logger.LogTrace(ex, "Caught exception while waiting for device to be ready. Retrying.");
                }

                await Task.Delay(100, cancellationToken)
                          .ConfigureAwait(false);
            }

            throw new Exception($"Device not ready after {300}ms");
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