using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using DfuSharp;
using LibUsbDotNet;
using Meadow.CLI;

namespace MeadowCLI
{
    public static class DfuUpload
    {
        static int _osAddress = 0x08000000;
        static string _usbStmName = "STM32  BOOTLOADER";

        public static void FlashOS(string filename = "")
        {
        start:
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
            }
            else
            {
                // if filename isn't specified fallback to download path
                if (string.IsNullOrEmpty(filename))
                {
                    DownloadManager flashManager = new DownloadManager();
                    filename = Path.Combine(flashManager.FirmwareDownloadsFilePath, flashManager.OSFilename);
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

                try
                {
                    var process = Process.Start("dfu-util", $"-a 0 -S {serial} -D {filename} -s {_osAddress}");
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    // dfu-util isn't installed
                    if (ex.Message.Contains("cannot find"))
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Unix)
                        {
                            Console.WriteLine("dfu-util not found, to install run: brew install dfu-util");
                        }
                        else
                        {
                            Console.Write("dfu-util not found. Do you want to install it? (Y/N): ");
                            var key = Console.ReadKey();
                            Console.WriteLine();
                            if (key.Key == ConsoleKey.Y)
                            {
                                var downloadManager = new DownloadManager();
                                if (downloadManager.DownloadDfuUtil(Environment.Is64BitOperatingSystem).Result)
                                {
                                    goto start;
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"There was a problem executing dfu-util: {ex.Message}");
                    }

                    return;
                }

                try
                {
                    // TODO: need to get the serial number and target the appropriate device to support multiple devices
                    DfuContext.Init();
                    var devices = DfuContext.Current.GetDevices();
                    devices[0].Clear();
                    devices[0].Reset();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Manually reset or reconnect to start device in runtime mode");
                }
            }
        }
    }
}
