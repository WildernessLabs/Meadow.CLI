using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using Meadow.CLI;
using Meadow.CLI.Core;

namespace MeadowCLI
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

        public static void FlashOS(string filename = "", UsbRegistry device = null)
        {
            if (device == null)
                device = GetDevice();

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

            var serial = GetDeviceSerial(device);

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
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "dfu-util";
                    process.StartInfo.Arguments = $"-a 0 -S {serial} -D \"{filename}\" -s {_osAddress}:leave";
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was a problem executing dfu-util: {ex.Message}");
                return;
            }
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
