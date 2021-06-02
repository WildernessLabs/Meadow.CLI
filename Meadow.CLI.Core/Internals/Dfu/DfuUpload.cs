using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibUsbDotNet;

namespace Meadow.CLI.Core.Internals.Dfu
{
    public static class DfuUpload
    {
        private const int ERROR_FILE_NOT_FOUND = 2;
        private const int ERROR_PATH_NOT_FOUND = 3;

        static int _osAddress = 0x08000000;
        static string _usbStmName = "STM32  BOOTLOADER";

        public static void FlashOS(string filename = "")
        {
            var allDevices = UsbDevice.AllDevices;
            if (allDevices.Count(x => x.Name == _usbStmName) > 1)
            {
                Console.WriteLine("More than one DFU device found, please connect only one and try again.");
                return;
            }

            var device = UsbDevice.AllDevices.SingleOrDefault(x => x.Name == _usbStmName);
            if (device == null)
            {
                Console.WriteLine("Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
                return;
            }
            else
            {
                // if filename isn't specified fallback to download path
                if (string.IsNullOrEmpty(filename))
                {
                    filename = Path.Combine(DownloadManager.FirmwareDownloadsFilePath, DownloadManager.OSFilename);
                }

                if (!File.Exists(filename))
                {
                    Console.WriteLine("Please specify valid --File or --Download latest");
                    return;
                }
                else
                {
                    Console.WriteLine($"Flashing OS with {filename}");
                }

                string serial = string.Empty;

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var deviceID = device.DeviceProperties["DeviceID"].ToString();
                    serial = deviceID.Substring(deviceID.LastIndexOf("\\") + 1);
                }
                else
                {
                    serial = device.DeviceProperties["SerialNumber"].ToString();
                }

                var dfuUtilVersion = GetDfuUtilVersion();

                if (string.IsNullOrEmpty(dfuUtilVersion))
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        Console.WriteLine("dfu-util not found. To install, run in administrator mode: meadow --InstallDfuUtil");
                    }
                    else
                    {
                        Console.WriteLine("dfu-util not found. To install run: brew install dfu-util");
                    }
                    return;
                }
                else if (dfuUtilVersion != "0.10")
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        Console.WriteLine("dfu-util update required. To install, run in administrator mode: meadow --InstallDfuUtil");
                    }
                    else
                    {
                        Console.WriteLine("dfu-util update required. To install, run: brew upgrade dfu-util");
                    }
                    return;
                }

                try
                {
                    using var process = new Process
                                        {
                                            StartInfo =
                                            {
                                                FileName = "dfu-util",
                                                Arguments =
                                                    $"-a 0 -S {serial} -D \"{filename}\" -s {_osAddress}:leave",
                                                UseShellExecute = false
                                            }
                                        };

                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"There was a problem executing dfu-util: {ex.Message}");
                    return;
                }
            }
        }

        private static string GetDfuUtilVersion()
        {
            try
            {
                using var process = new Process
                                    {
                                        StartInfo =
                                        {
                                            FileName = "dfu-util",
                                            Arguments = $"--version",
                                            UseShellExecute = false,
                                            RedirectStandardOutput = true
                                        }
                                    };

                process.Start();

                var reader = process.StandardOutput;
                var output = reader.ReadLine();
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
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == ERROR_FILE_NOT_FOUND || ex.NativeErrorCode == ERROR_PATH_NOT_FOUND)
                {
                    return string.Empty;
                }

                throw ex;
            }
        }
    }
}
