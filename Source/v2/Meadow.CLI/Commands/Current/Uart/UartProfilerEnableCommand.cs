using System;
using System.IO.Ports;
using System.IO;
using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart profiler enable", Description = "Enables profiling data output to UART")]
public class UartProfilerEnableCommand : BaseDeviceCommand<UartProfilerEnableCommand>
{
    public UartProfilerEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    void ReadSerial()
    {
       // Define serial port and baudrate
        string serialPort = "/dev/tty.usbserial-1120"; // TODO: Remove this hardcoded serial port
        int baudRate = 115200; // TODO: Remove this hardcoded baud rate

        // Define the header bytes
        byte[] header = { 0x01, 0x5A, 0x50, 0x4D }; // TODO: Confirm if these header bytes are enough
        byte[] buffer = new byte[header.Length];
        int headerIndex = 0;

        SerialPort port = new SerialPort(serialPort, baudRate);

        try
        {
            port.Open();
            Logger?.LogInformation("Serial connection opened successfully.");

            // Step 1: Read until header sequence is found
            bool headerFound = false;
            while (!headerFound)
            {
                int data = port.ReadByte();
                if (data != -1)
                {
                    // Check if received byte matches the corresponding byte in the header sequence
                    if (data == header[headerIndex])
                    {
                        Logger?.LogTrace($"Profiler data header index {headerIndex} has been found {(byte)data}");
                        buffer[headerIndex] = (byte)data;
                        headerIndex++;
                        // If the entire header sequence is found, set headerFound to true
                        if (headerIndex == header.Length)
                        {
                            Logger?.LogInformation("Profiling data header found! Writing to the output.mlpd file..."); //
                            headerFound = true;
                        }
                    }
                    else
                    {
                        // Reset header index if received byte does not match
                        headerIndex = 0;
                    }
                }
            }

            if (!headerFound)
            {
                Logger?.LogError("Profiling data header hasn't been found!");

                return;
            }
            // Step 2: After the header sequence is found, write the profiling data to the output file
            using (FileStream outputFile = new FileStream("output.mlpd", FileMode.Create)) // TODO: Remove this hardcode path
            {
                var totalBytesWritten = 0;

                foreach (byte b in buffer)
                {
                    outputFile.WriteByte(b);
                    totalBytesWritten++;
                }
                while (true)
                {
                    int data = port.ReadByte();
                    if (data != -1)
                    {
                        outputFile.WriteByte((byte)data);
                        totalBytesWritten++;
                    }
                    if (totalBytesWritten % 1024 == 0)
                    {
                        Logger?.LogInformation($"{totalBytesWritten} bytes written..."); // TODO: Enhance it
                    }
                }
            }
        }
        catch (IOException ex)
        {
            Logger?.LogInformation("Failed to open serial port: " + ex.Message);
        }
        finally
        {
            // Ensure the serial port is closed
            if (port.IsOpen)
                port.Close();
        }
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            return;
        }

        connection.DeviceMessageReceived += (s, e) =>
        {
            Logger?.LogInformation(e.message);
        };

        Logger?.LogInformation("Setting UART to output profiler binaries...");

        await connection.Device.UartProfilerEnable(CancellationToken);

        Logger?.LogInformation("Reading profiler binaries on UART...");

        ReadSerial();
    }
}