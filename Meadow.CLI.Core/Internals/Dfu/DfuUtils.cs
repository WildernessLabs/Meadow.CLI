using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using LibUsbDotNet;
using LibUsbDotNet.Main;

using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Logging;

namespace Meadow.CLI.Core.Internals.Dfu
{
    public static class DfuUtils
    {
        static int _osAddress = 0x08000000;
        static string _usbStmName = "STM32  BOOTLOADER";

        public static bool CheckForValidDevice()
        {
            try
            {
                GetDevice();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static UsbRegistry GetDevice()
        {
            var allDevices = UsbDevice.AllDevices;
            if (allDevices.Count(x => x.Name == _usbStmName) > 1)
            {
                throw new MultipleDfuDevicesException("More than one DFU device found, please connect only one and try again.");

            }

            var device = UsbDevice.AllDevices.SingleOrDefault(x => x.Name == _usbStmName);
            if (device == null)
            {
                throw new DeviceNotFoundException("Device not found. Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
            }

            return device;
        }

        public static string GetDeviceSerial(UsbRegistry device)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    {
                        var deviceID = device.DeviceProperties["DeviceID"].ToString();
                        return deviceID.Substring(deviceID.LastIndexOf("\\") + 1);
                    }
                default:
                    return device.DeviceProperties["SerialNumber"].ToString();
            }
        }

        public static async Task<bool> DfuFlashAsync(string filename = "", UsbRegistry? device = null, ILogger? logger = null)
        {
            logger ??= NullLogger.Instance;
            device ??= GetDevice();

            // if filename isn't specified fallback to download path
            if (string.IsNullOrEmpty(filename))
            {
                filename = Path.Combine(DownloadManager.FirmwareDownloadsFilePath, DownloadManager.OsFilename);
            }

            if (!File.Exists(filename))
            {
                logger.LogError("Please specify valid --File or download the latest with: meadow download os");
                return false;
            }
            else
            {
                logger.LogInformation($"Flashing OS with {filename}");
            }

            var serial = GetDeviceSerial(device);

            var dfuUtilVersion = GetDfuUtilVersion();
            logger.LogDebug("Detected OS: {os}", RuntimeInformation.OSDescription);

            if (string.IsNullOrEmpty(dfuUtilVersion))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    logger.LogError("dfu-util not found. To install, run in administrator mode: meadow install dfu-util");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    logger.LogError("dfu-util not found. To install run: brew install dfu-util");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    logger.LogError("dfu-util not found. Install using package manager, for example: apt install dfu-util");
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
                var startInfo = new ProcessStartInfo(
                                    "dfu-util",
                                    $"-a 0 -S {serial} -D \"{filename}\" -s {_osAddress}:leave")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new Exception("Failed to start dfu-util");

                var informationLogger = logger != null
                                     ? Task.Factory.StartNew(
                                         () =>
                                         {
                                             while (process.HasExited == false)
                                             {
                                                 var logLine = process.StandardOutput.ReadLine();
                                                 // Ignore empty output
                                                 if (logLine == null)
                                                     continue;
                                                 
                                                 if (logLine.Contains("%"))
                                                 {
                                                     var operation = logLine.Substring(0,
                                                         logLine.IndexOf("\t", StringComparison.Ordinal)).Trim();
                                                     var progressBarEnd = logLine.IndexOf("]", StringComparison.Ordinal) + 1;
                                                     var progress = logLine.Substring(progressBarEnd, logLine.IndexOf("%", StringComparison.Ordinal) - progressBarEnd + 1).TrimStart();
                                                     Console.SetCursorPosition(0, Console.CursorTop);
                                                     if (progress != "100%")
                                                         Console.Write($"{operation} {progress}");
                                                 }
                                                 else
                                                 {
                                                     logger.LogInformation(logLine);
                                                 }
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
            catch (Exception ex)
            {
                logger.LogError($"There was a problem executing dfu-util: {ex.Message}");
                return false;
            }

            return true;
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
            catch (Exception ex)
            {
                if (ex.Message.Contains("cannot find") || ex.Message.Contains("No such file or directory"))
                {
                    return string.Empty;
                }
                else
                {
                    throw ex;
                }
            }
        }
    }
}
