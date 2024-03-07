using CliFx.Attributes;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;
using System.IO.Ports;

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

    private void StartNewFile(string outputPath, ref FileStream outputFile, byte[] header, ref int headerIndex, ref int totalBytesWritten, ref int headerFileCount)
    {
        if (outputFile != null)
        {
            outputFile.Close();
            outputFile.Dispose();
        }
        outputFile = new FileStream(outputPath, FileMode.Create);
        totalBytesWritten = 0;
        headerIndex = 0;
        headerFileCount++;
        foreach (var headerByte in header)
        {
            outputFile.WriteByte(headerByte);
            totalBytesWritten++;
        }
    }

    private void ReadAndSaveSerialData()
    {
        // Define the equivalent header bytes sequence to the 32-bit representation of 0x4D505A01
        //  according to the Mono Profiler LOG_VERSION_MAJOR 3 LOG_VERSION_MINOR 0 LOG_DATA_VERSION 17
        var header = new byte[] { 0x01, 0x5A, 0x50, 0x4D };
        var headerIndex = 0;
        var totalBytesWritten = 0;
        var headerFileCount = 0;

        var defaultDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        outputDirectory ??= defaultDirectory;
        var outputPath = Path.Combine(outputDirectory, "output.mlpd");

        SerialPort port = new SerialPort(SerialInterface, SerialConnection.DefaultBaudRate);
        FileStream outputFile = null;

        try
        {
            port.Open();
            Logger?.LogInformation("Serial connection opened successfully.");

            while (true)
            {
                int data = port.ReadByte();
                if (data != -1)
                {
                    if (headerFileCount == 0)
                    {
                        // Check if the received data matches the header sequence
                        if (data == header[headerIndex])
                        {
                            headerIndex++;
                            // If the entire header sequence is found, start writing to a file
                            if (headerIndex == header.Length)
                            {
                                Logger?.LogInformation($"Profiling data header found! Writing to {outputPath}...");
                                StartNewFile(outputPath, ref outputFile, header, ref headerIndex, ref totalBytesWritten, ref headerFileCount);
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
                        // Writing to file after a header is found
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
                                var newOutputPath = outputDirectory + "output_" + headerFileCount + ".mlpd";
                                Logger?.LogInformation($"New profiling data header found! Writing to {newOutputPath}...");
                                StartNewFile(newOutputPath, ref outputFile, header, ref headerIndex, ref totalBytesWritten, ref headerFileCount);
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