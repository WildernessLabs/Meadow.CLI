using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using Meadow.CLI.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Internals.Dfu
{
    public static class DfuUtils
    {
        static int _osAddress = 0x08000000;
#if WIN_10
        static string _usbStmName = "STM32  BOOTLOADER";
#endif
        static readonly int _usbBootLoaderVenderID = 1155; // Equivalent to _usbStmName but for the LibUsbDotNet 3.x

        public static string? LastSerialNumber { get; private set; } = "";

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
#if WIN_10
        public static UsbRegistry GetDeviceInBootloaderMode()
#else
        public static IUsbDevice GetDeviceInBootloaderMode()
#endif
        {
            var allDevices = GetDevicesInBootloaderMode();
            if (allDevices.Count() > 1)
            {
                throw new MultipleDfuDevicesException("More than one DFU device found, please connect only one and try again.");
            }

            var device = allDevices.SingleOrDefault();
            if (device == null)
            {
                throw new DeviceNotFoundException("Device not found. Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
            }

            return device;
        }
#if WIN_10
        public static IEnumerable<UsbRegistry> GetDevicesInBootloaderMode()
#else
        public static IEnumerable<IUsbDevice> GetDevicesInBootloaderMode()
#endif
        {
#if WIN_10

            var allDevices = UsbDevice.AllDevices;
            IEnumerable<UsbRegistry> ourDevices;

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    ourDevices = allDevices.Where(d => d.DeviceProperties["FriendlyName"].ToString() == _usbStmName);
                    break;
                default:
                    ourDevices = allDevices.Where(d => d.DeviceProperties["DeviceDesc"].ToString() == _usbStmName);
                    break;
            }

            if (ourDevices.Count() < 1)
            {
                throw new DeviceNotFoundException("No Devices found. Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
            }
            return ourDevices;
#else
            using (UsbContext context = new UsbContext())
            {
                var allDevices = context.List();
                var ourDevices = allDevices.Where(d => d.Info.VendorId == _usbBootLoaderVenderID);
                if (ourDevices.Count() < 1)
                {
                    throw new DeviceNotFoundException("No Devices found. Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
                }
                return ourDevices;
            }
#endif
        }

#if WIN_10
        public static string GetDeviceSerial(UsbRegistry device)
        {
            if (device != null && device.DeviceProperties != null)
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Win32NT:
                        var deviceID = device.DeviceProperties["DeviceID"].ToString();
                        if (!string.IsNullOrWhiteSpace(deviceID))
                            return deviceID.Substring(deviceID.LastIndexOf("\\") + 1);
                        else
                            return string.Empty;
                    default:
                        return device.DeviceProperties["SerialNumber"].ToString();
                }
            }
            else
                return string.Empty;
        }
#else
        public static string? GetDeviceSerial(IUsbDevice device)
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
#endif

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

        static void FormatDfuOutput(string logLine, ILogger? logger, DfuFlashFormat format = DfuFlashFormat.Percent)
        {
            if(format == DfuFlashFormat.Full)
            {
                logger?.LogInformation(logLine);
            }
            else if(format == DfuFlashFormat.Percent)
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

                Console.Write(logLine.Contains("%")?"\r":"\r\n");
            }
        }

        public static Task<bool> FlashVersion(string? version, ILogger? logger = null, DfuFlashFormat format = DfuFlashFormat.Percent)
        {
            if (!string.IsNullOrWhiteSpace(version))
            {
                var fileName = Path.Combine(DownloadManager.FirmwarePathForVersion(version), DownloadManager.OsFilename);

                return FlashFile(fileName: fileName, logger: logger, format: format);
            }
            else
                return Task.FromResult(false);
        }

        public static Task<bool> FlashLatest(ILogger? logger = null, DfuFlashFormat format = DfuFlashFormat.Percent)
        {
            var fileName = Path.Combine(DownloadManager.FirmwareDownloadsFilePath, DownloadManager.OsFilename);

            return FlashFile(fileName: fileName, logger: logger, format: DfuUtils.DfuFlashFormat.ConsoleOut);
        }
#if WIN_10
        public static async Task<bool> FlashFile(string fileName, UsbRegistry? device = null, ILogger? logger = null, DfuFlashFormat format = DfuFlashFormat.Percent)
#else
        public static async Task<bool> FlashFile(string fileName, IUsbDevice? device = null, ILogger? logger = null, DfuFlashFormat format = DfuFlashFormat.Percent)
#endif
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
                logger.LogError($"Unable to find file '{DownloadManager.OsFilename}'. Please specify valid --File or download the latest with: meadow download os");
                return false;
            }
            else
            {
                logger.LogInformation($"Flashing OS with {fileName}");
            }

            LastSerialNumber = GetDeviceSerial(device);

            var dfuUtilVersion = new System.Version(GetDfuUtilVersion());
            logger.LogDebug("Detected OS: {os}", RuntimeInformation.OSDescription);

            if (dfuUtilVersion == null)
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
                    logger.LogError("dfu-util not found - install using package manager, for example: `apt install dfu-util` or the equivalent for your Linux distribution");
                }
                return false;
            }
            else if (dfuUtilVersion.CompareTo(new System.Version("0.11")) < 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    logger.LogError("dfu-util update required. To update, run in administrator mode: `meadow install dfu-util`");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    logger.LogError("dfu-util update required. To update, run: `brew upgrade dfu-util`");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    logger.LogError("dfu-util update required. To update , run: `apt upgrade dfu-util` or the equivalent for your Linux distribution");
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

        static async Task RunDfuUtil(string args, ILogger? logger, DfuFlashFormat format = DfuFlashFormat.Percent)
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
                    if (reader != null)
                    {
                        string? output = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(output) && output.StartsWith("dfu-util"))
                        {
                            var split = output.Split(new char[] { ' ' });
                            if (split.Length == 2)
                            {
                                return split[1];
                            }
                        }

                        process.WaitForExit();
                    }
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
}
