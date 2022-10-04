using Meadow.CLI.Core.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public class MeadowConnection : IMeadowConnection
    {
        public event ConnectionStateHandler ConnectionStateChanged = delegate { };

        private const int DefaultBaudRate = 115200;
        private const int DefaultTimeout = 5000;
        private const int CommsTimeoutSeconds = 2;

        private readonly SerialPort _port;

        public IMeadowDevice? Device { get; private set; }
        public string Name { get; }
        public ILogger? Logger { get; }

        public bool IsConnected => _port?.IsOpen ?? false;
        public bool AutoReconnect { get; set; } = false;

        internal MeadowConnection(string portName, ILogger? logger)
        {
            Logger = logger;
            Name = portName;
            _port = new SerialPort(Name, DefaultBaudRate, Parity.None, 8, StopBits.One);
            _port.ReadTimeout = DefaultTimeout;
            _port.WriteTimeout = DefaultTimeout;

            Task.Run(ConnectionStateMonitorProc);
        }

        public Task<bool> WaitForConnection(TimeSpan timeout)
        {
            var keepTrying = true;

            var tasks = new List<Task>();

            if (timeout.TotalMilliseconds > 0)
            {
                tasks.Add(Task.Delay(timeout));
            }

            tasks.Add(Task.Run(async () =>
            {
                while (keepTrying)
                {
                    if (IsConnected) return;
                    await Task.Delay(500);
                }
            }));

            Task.WaitAny(tasks.ToArray());

            keepTrying = false;

            return Task.FromResult(IsConnected);
        }

        public void Connect()
        {
            if (_port.IsOpen)
            {
                return;
            }

            try
            {
                _port.Open();
                Device = new MeadowSerialDevice_Old(Name, _port, Logger);
            }
            catch (FileNotFoundException fnf)
            {
                Logger?.LogTrace($"Unable to open serial port: {fnf.Message}");
            }
            catch (Exception ex)
            {
                Logger?.LogTrace($"Unable to open serial port: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (!_port.IsOpen)
            {
                return;
            }

            _port.Close();

            Device = null;
        }

        private async Task ConnectionStateMonitorProc()
        {
            var lastState = _port.IsOpen;

            while (true)
            {
                try
                {
                    var nowState = _port.IsOpen;
                    if (lastState != nowState)
                    {
                        lastState = nowState;

                        if (nowState && Device != null)
                        {
                            // wait a bit - the serial port can connect before the Meadow is ready
                            await Task.Delay(1000);
                            _ = await Device.GetDeviceInfoAsync(TimeSpan.FromSeconds(2));
                        }

                        ConnectionStateChanged.Invoke(this, nowState);
                    }

                    if (!IsConnected && AutoReconnect)
                    {
                        Connect();
                    }
                }
                catch (TimeoutException)
                {
                    // this is a fallback
                    lastState = false;
                }
                catch (Exception ex)
                {
                    lastState = false;
                }

                await Task.Delay(1000);
            }
        }

    }
}
