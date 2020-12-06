using System;
using System.IO;
using DfuSharp;
using Meadow.CLI;

namespace MeadowCLI
{
    public static class DfuUpload
    {
        static int os_address = 0x08000000;

        static int uploadedByteCount = 0;
        static int totalBytes = 0;

        public static void FlashOS(string filename = "")
        {
            DfuContext.Init();
            var devices = DfuContext.Current.GetDevices();

            if (devices.Count < 1)
            {
                Console.WriteLine("Connect a device in bootloader mode. If the device is in bootloader mode, please update the device driver. See instructions at https://wldrn.es/usbdriver");
            }
            else
            {
                if (string.IsNullOrEmpty(filename))
                {
                    DownloadManager flashManager = new DownloadManager();
                    filename = Path.Combine(flashManager.FirmwareDownloadsFilePath, flashManager.osFilename);

                    if (File.Exists(filename))
                    {
                        Console.WriteLine($"Flashing {flashManager.osFilename} from latest download");
                    }
                }
                else
                {
                    if (File.Exists(filename))
                    {
                        var fi = new FileInfo(filename);
                        Console.WriteLine($"Flashing {fi.Name} from {fi.DirectoryName}");
                    }
                }

                if (!File.Exists(filename))
                {
                    Console.WriteLine("Please specify valid --File or --Download latest");
                    return;
                }

                devices[0].Uploading += Program_Uploading;
                Upload(devices[0], filename, os_address);
            }
        }

        private static void Upload(DfuDevice device, string path, int address)
        {
            FileInfo fi = new FileInfo(path);
            byte[] bytes = File.ReadAllBytes(path);
            totalBytes = bytes.Length;

            uploadedByteCount = 0;
            device.Clear();
            device.EraseSector((int)address);
            device.Upload(bytes, (int)address);
            device.Reset();
            Console.WriteLine("\rFlash Complete                     ");
        }

        private static void Program_Uploading(object sender, UploadingEventArgs e)
        {
            uploadedByteCount += e.BytesUploaded;

            Console.Write($"\r{(uploadedByteCount * 100 / totalBytes)}% complete");
        }
    }
}
