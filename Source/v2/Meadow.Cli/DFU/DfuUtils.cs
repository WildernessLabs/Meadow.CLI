using LibUsbDotNet.LibUsb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Meadow.CLI.Core.Internals.Dfu;

public static class DfuUtils
{
    private static int _osAddress = 0x08000000;
    private static string _usbStmName = "STM32  BOOTLOADER";
    private static int _usbBootLoaderVenderID = 1155; // Equivalent to _usbStmName but for the LibUsbDotNet 3.x

    public static string LastSerialNumber { get; private set; } = "";

    public static bool CheckForValidDevice()
    {
        try
        {
            GetDeviceInBootloaderMode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static IUsbDevice GetDeviceInBootloaderMode()
    {
        var allDevices = GetDevicesInBootloaderMode();
        if (allDevices.Count() > 1)
        {
            throw new Exception("More than one DFU device found, please connect only one and try again.");
        }

        var device = allDevices.SingleOrDefault();
        if (device == null)
        {
            throw new Exception("Device not found. Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
        }

        return device;
    }

    public static IEnumerable<IUsbDevice> GetDevicesInBootloaderMode()
    {
        using (UsbContext context = new UsbContext())
        {
            var allDevices = context.List();
            var ourDevices = allDevices.Where(d => d.Info.VendorId == _usbBootLoaderVenderID);
            if (ourDevices.Count() < 1)
            {
                throw new Exception("No Devices found. Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
            }
            return ourDevices;
        }
    }

    public static string GetDeviceSerial(IUsbDevice device)
    {
        var serialNumber = string.Empty;

        if (device != null)
        {
            device.Open();
            if (device.IsOpen)
            {
                serialNumber = device.Info?.SerialNumber;
                device.Close();
            }
        }

        return serialNumber;
    }

    public enum DfuFlashFormat
    {
        /// <summary>
        /// Percentage only
        /// </summary>
        Percent,
        /// <summary>
        /// Full console output, no formatting
        /// </summary>
        Full,
        /// <summary>
        /// Console.WriteLine for CLI - ToDo - remove
        /// </summary>
        ConsoleOut,
    }

    private static void FormatDfuOutput(string logLine, ILogger? logger, DfuFlashFormat format = DfuFlashFormat.Percent)
    {
        if (format == DfuFlashFormat.Full)
        {
            logger?.LogInformation(logLine);
        }
        else if (format == DfuFlashFormat.Percent)
        {
            if (logLine.Contains("%"))
            {
                var operation = logLine.Substring(0,
                    logLine.IndexOf("\t", StringComparison.Ordinal)).Trim();
                var progressBarEnd = logLine.IndexOf("]", StringComparison.Ordinal) + 1;
                var progress = logLine.Substring(progressBarEnd, logLine.IndexOf("%", StringComparison.Ordinal) - progressBarEnd + 1).TrimStart();
                if (progress != "100%")
                {
                    logger?.LogInformation(progress);
                }
            }
            else
            {
                logger?.LogInformation(logLine);
            }
        }
        else //Console out
        {
            Console.Write(logLine);

            Console.Write(logLine.Contains("%") ? "\r" : "\r\n");
        }
    }

    public static async Task<bool> FlashFile(string fileName, IUsbDevice? device = null, ILogger? logger = null, DfuFlashFormat format = DfuFlashFormat.Percent)
    {
        logger ??= NullLogger.Instance;
        device ??= GetDeviceInBootloaderMode();

        if (!File.Exists(fileName))
        {
            logger.LogError($"Unable to flash {fileName} - file or folder does not exist");
            return false;
        }

        if (!File.Exists(fileName))
        {
            logger.LogError($"Unable to find file '{fileName}'. Please specify valid --File or download the latest with: meadow download os");
            return false;
        }
        else
        {
            logger.LogInformation($"Flashing OS with {fileName}");
        }

        LastSerialNumber = GetDeviceSerial(device);

        var dfuUtilVersion = GetDfuUtilVersion();
        logger.LogDebug("Detected OS: {os}", RuntimeInformation.OSDescription);

        if (string.IsNullOrEmpty(dfuUtilVersion))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError("dfu-util not found - to install, run: `meadow install dfu-util` (may require administrator mode)");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                logger.LogError("dfu-util not found - to install run: `brew install dfu-util`");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                logger.LogError("dfu-util not found - install using package manager, for example: `apt install dfu-util`");
            }
            return false;
        }
        else if (dfuUtilVersion != "0.10")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError("dfu-util update required. To install, run in administrator mode: meadow install dfu-util");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                logger.LogError("dfu-util update required. To install, run: brew upgrade dfu-util");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (dfuUtilVersion != "0.9")
                    return false;
            }
            else
            {
                return false;
            }
        }

        try
        {
            var args = $"-a 0 -S {LastSerialNumber} -D \"{fileName}\" -s {_osAddress}:leave";

            await RunDfuUtil(args, logger, format);
        }
        catch (Exception ex)
        {
            logger.LogError($"There was a problem executing dfu-util: {ex.Message}");
            return false;
        }

        return true;
    }

    private static async Task RunDfuUtil(string args, ILogger? logger, DfuFlashFormat format = DfuFlashFormat.Percent)
    {
        var startInfo = new ProcessStartInfo("dfu-util", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo);

        if (process == null)
        {
            throw new Exception("Failed to start dfu-util");
        }

        var informationLogger = logger != null
                             ? Task.Factory.StartNew(
                                 () =>
                                 {
                                     var lastProgress = string.Empty;

                                     while (process.HasExited == false)
                                     {
                                         var logLine = process.StandardOutput.ReadLine();
                                         // Ignore empty output
                                         if (logLine == null)
                                             continue;

                                         FormatDfuOutput(logLine, logger, format);
                                     }
                                 }) : Task.CompletedTask;

        var errorLogger = logger != null
                                    ? Task.Factory.StartNew(
                                        () =>
                                        {
                                            while (process.HasExited == false)
                                            {
                                                var logLine = process.StandardError.ReadLine();
                                                logger.LogError(logLine);
                                            }
                                        }) : Task.CompletedTask;
        await informationLogger;
        await errorLogger;
        process.WaitForExit();
    }

    private static string GetDfuUtilVersion()
    {
        try
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "dfu-util";
                process.StartInfo.Arguments = $"--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                var reader = process.StandardOutput;
                string output = reader.ReadLine();
                if (output.StartsWith("dfu-util"))
                {
                    var split = output.Split(new char[] { ' ' });
                    if (split.Length == 2)
                    {
                        return split[1];
                    }
                }

                process.WaitForExit();
                return string.Empty;
            }
        }
        catch (Win32Exception ex)
        {
            switch (ex.NativeErrorCode)
            {
                case 0x0002: // ERROR_FILE_NOT_FOUND
                case 0x0003: // ERROR_PATH_NOT_FOUND
                case 0x000F: // ERROR_INVALID_DRIVE
                case 0x0014: // ERROR_BAD_UNIT
                case 0x001B: // ERROR_SECTOR_NOT_FOUND
                case 0x0033: // ERROR_REM_NOT_LIST
                case 0x013D: // ERROR_MR_MID_NOT_FOUND
                    return string.Empty;

                default:
                    throw;
            }
        }
    }
}
