using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public partial class MeadowSerialDevice : MeadowLocalDevice
    {
        private readonly string _serialPortName;
        public SerialPort SerialPort { get; private set; }

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

        public override void Dispose()
        {
            Logger.LogTrace("Disposing SerialPort");
            SerialPort.Dispose();
        }

        public override async Task WriteAsync(byte[] encodedBytes, int encodedToSend, CancellationToken cancellationToken = default)
        {
            if (SerialPort == null)
                throw new NotConnectedException();

            if (SerialPort.IsOpen == false)
            {
                Logger.LogDebug("Port is not open, attempting reconnect.");
                await AttemptToReconnectToMeadow(cancellationToken);
            }

            SerialPort.Write(encodedBytes, 0, encodedToSend);
        }

        public override bool Initialize(CancellationToken cancellationToken = default)
        {
            if (!SerialPort.IsOpen)
            {
                SerialPort.Open();
                SerialPort.BaseStream.ReadTimeout = 0;
            }

            return SerialPort.IsOpen;
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

            return port;
        }

        internal async Task<bool> AttemptToReconnectToMeadow(CancellationToken cancellationToken = default)
        {
            var delayCount = 20; // 10 seconds
            while (true)
            {
                await Task.Delay(500, cancellationToken)
                          .ConfigureAwait(false);

                var portOpened = Initialize(cancellationToken);

                if (portOpened)
                {
                    Logger.LogDebug("Device successfully reconnected");
                    await Task.Delay(2000, cancellationToken)
                              .ConfigureAwait(false);

                    return true;
                }

                if (delayCount-- == 0)
                    throw new NotConnectedException();
            }
        }
    }
}