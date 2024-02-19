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

    [CommandOption("outputDirectory", 'o', Description = $"Set the profiling data output directory path", IsRequired = false)]
    public string? outputDirectory { get; set; }

    public UartProfilerEnableCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

private void ReadAndSaveSerialData()
{
    // Define the equivalent header bytes sequence to the 32-bit representation of 0x4D505A01
    //  according to the Mono Profiler LOG_VERSION_MAJOR 3 LOG_VERSION_MINOR 0 LOG_DATA_VERSION 17
    byte[] header = { 0x01, 0x5A, 0x50, 0x4D };
    int headerIndex = 0;
    string defaultDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    outputDirectory ??= defaultDirectory;
    string outputPath = Path.Combine(outputDirectory, "output.mlpd");

    SerialPort port = new SerialPort(SerialInterface, SerialConnection.DefaultBaudRate);

    FileStream outputFile = null;
    bool writingToFile = false;
    int totalBytesWritten = 0;
    int headerFoundTimes = 0;

    try
    {
        port.Open();
        Logger?.LogInformation("Serial connection opened successfully.");

        while (true)
        {
            int data = port.ReadByte();
            if (data != -1)
            {
                if (!writingToFile)
                {
                    // Check if the received data matches the header sequence
                    if (data == header[headerIndex])
                    {
                        headerIndex++;
                        // If the entire header sequence is found, start writing to a file
                        if (headerIndex == header.Length)
                        {
                            Logger?.LogInformation($"Profiling data header found! Writing to {outputPath}...");
                            outputFile = new FileStream(outputPath, FileMode.Create);
                            foreach (var headerByte in header)
                            {
                                outputFile.WriteByte((byte)headerByte);
                            }
                            writingToFile = true;
                            totalBytesWritten += 4;
                            headerIndex = 0;
                            headerFoundTimes++;
                        }
                    }
                    else
                    {
                        // Reset header index if received byte does not match
                        headerIndex = 0;
                    }
                }
                else
                {

                    // Writing to file after header is found
                    outputFile.WriteByte((byte)data);
                    totalBytesWritten++;

                    // Check for a new header while writing to a file
                    if (data == header[headerIndex])
                    {
                        headerIndex++;
                        if (headerIndex == header.Length)
                        {
                            // Close the current file, start writing to a new file, and reset counters
                            //  to avoid corrupted profiling data (e.g. device reset while profiling)
                            outputFile.Close();
                            outputFile.Dispose();
                            headerFoundTimes++;
                            var newOutputPath = outputDirectory + "output_" + headerFoundTimes + ".mlpd";
                            outputFile = new FileStream(newOutputPath, FileMode.Create);
                            Logger?.LogInformation($"New profiling data header found! Writing to {newOutputPath}...");
                            headerIndex = 0;
                            foreach (var headerByte in header)
                            {
                                outputFile.WriteByte((byte)headerByte);
                            }
                            totalBytesWritten = 4; 
                        }
                    }
                    else
                    {
                        // Reset header index if received byte does not match
                        headerIndex = 0;
                    }

                    // Log bytes written periodically to not spam the console
                    if (totalBytesWritten % (4 * 1024) == 0)
                    {
                        Logger?.LogInformation($"{totalBytesWritten} bytes written...");
                    }
                }
            }
        }
    }
    catch (IOException ex)
    {
        Logger?.LogError("Failed to open serial port: " + ex.Message);
    }
    finally
    {
        outputFile?.Close();
        outputFile?.Dispose();
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

        Logger?.LogInformation("Setting UART to only output profiling data...");

        await connection.Device.UartProfilerEnable(CancellationToken);

        Logger?.LogInformation("Reseting Meadow device...");

        await connection.ResetDevice();

        Logger?.LogInformation($"Attempting to open a serial connection on {SerialInterface} UART interface...");

        ReadAndSaveSerialData();
    }
}