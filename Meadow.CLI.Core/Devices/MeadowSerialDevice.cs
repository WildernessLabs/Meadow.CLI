﻿using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Devices
{
    public class MeadowSerialDevice : MeadowLocalDevice
    {
        private readonly string _serialPortName;
        public SerialPort? SerialPort { get; private set; }

        const int SERIAL_PORT_TIMEOUT = 5;

        private readonly object lockObject = new object();

        public MeadowSerialDevice(string serialPortName, ILogger? logger = null)
            : this(serialPortName, OpenSerialPort(serialPortName), logger)
        {
        }

        internal MeadowSerialDevice(string serialPortName,
                                   SerialPort serialPort,
                                   ILogger? logger = null)
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
                Logger?.LogTrace("Disposing SerialPort");

                if (SerialPort != null)
                {
                    lock (lockObject)
                    {
                        if (SerialPort.IsOpen)
                        {
                            SerialPort.Close();
                        }
                        SerialPort.Dispose();
                        SerialPort = null;
                    }
                }
            }
        }

        public override async Task Write(byte[] encodedBytes, int encodedToSend, CancellationToken cancellationToken = default)
        {
            if (SerialPort == null || SerialPort.IsOpen == false)
            {
                Logger.LogDebug("SerialPort is null or not open.");
                throw new DeviceDisconnectedException();
            }

            await SerialPort.BaseStream.WriteAsync(encodedBytes, 0, encodedToSend, cancellationToken);
        }

        public override async Task<bool> Initialize(CancellationToken cancellationToken)
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
                        await Task.Delay(1000, cancellationToken);

                        DeviceInfo = await GetDeviceInfo(TimeSpan.FromSeconds(5), cancellationToken);

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
                //ToDo: Adrian - review - increased delay from 100ms to 500ms
                await Task.Delay(500, cancellationToken);
            }

            throw new Exception($"Device not ready after {initTimeout}s");
        }

        private static SerialPort OpenSerialPort(string portName)
        {
            if (string.IsNullOrEmpty(portName))
            {
                throw new ArgumentException("Serial Port name cannot be empty");
            }

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

            int retries = 10;

            for (int i = 0; i < retries; i++)
            {
                try
                {   //on Windows the port can be slow to release after disposing 
                    port.Open();
                    port.BaseStream.ReadTimeout = 0;
                    break;
                }
                catch (FileNotFoundException)
                {
                    throw new Exception($"Serial port '{portName}' not found");
                }
                catch (UnauthorizedAccessException uae)
                {
                    if (i == retries - 1)
                    {
                        throw new Exception($"{uae.Message} Another application may have access to '{portName}'. ");
                    }
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    // We don't know what happened, best to bail and let the user know.
                    throw new Exception($"Unable to open port '{portName}'. {ex.Message}");
                }
            }

            return port;
        }
    }