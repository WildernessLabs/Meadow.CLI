using System;
using System.IO.Ports;
using System.IO;
using CliFx.Attributes;
using Microsoft.Extensions.Logging;
using Meadow.Hcom;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("uart profiler enable", Description = "Enables profiling data output to UART")]
public class UartProfilerEnableCommand : BaseDeviceCommand<UartProfilerEnableCommand>
{
    [CommandOption("interface", 'i', Description = $"Set the serial interface to read the profiling data via COM1", IsRequired = true)]
    public string? SerialInterface { get; set; }

    [CommandOption("outputPath", 'o', Description = $"Set the profiling data output path", IsRequired = false)]
    public string? OutputPath { get; set; }

    public UartProfilerEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    void ReadSerial()
    {
        // Define the equivalent header bytes sequence to the 32-bit representation of 0x4D505A01
        byte[] header = { 0x01, 0x5A, 0x50, 0x4D };
        byte[] buffer = new byte[header.Length];
        int headerIndex = 0;
        string directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string defaultOutputPath = Path.Combine(directory, "output.mlpd");
        OutputPath ??= defaultOutputPath;

        SerialPort port = new SerialPort(SerialInterface, SerialConnection.DefaultBaudRate);

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
                            Logger?.LogInformation($"Profiling data header found! Writing the {OutputPath}..."); //
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
            using (FileStream outputFile = new FileStream(OutputPath, FileMode.Create))
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
                    if (totalBytesWritten % (4 * 1024) == 0)
                    {
                        Logger?.LogInformation($"{totalBytesWritten} bytes written...");
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

        Logger?.LogInformation("Setting UART to output profiling data...");

        await connection.Device.UartProfilerEnable(CancellationToken);

        Logger?.LogInformation("Reseting Meadow device...");

        await connection.ResetDevice();

        Logger?.LogInformation($"Attempting to open a serial connection on {SerialInterface} UART interface...");

        ReadSerial();
    }
}