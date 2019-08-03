using System;
using System.IO;
using DfuSharp;

namespace MeadowCLI
{
    public static class DfuUpload
    {
        static string os = "nuttx.bin";
        static string user = "nuttx_user.bin";

        static int os_address = 0x08000000;
        static int user_address = 0x08040000;

        static int uploadedByteCount = 0;
        static int totalBytes = 0;

        public static void FlashNuttx(string dfuOsPath, string dfuUserPath)
        {
            DfuContext.Init();
            var devices = DfuContext.Current.GetDevices();

            if (devices.Count < 1)
            {
                Console.WriteLine("Attach a device in DFU mode, mofo.");
            }
            else
            {
                if (!string.IsNullOrEmpty(dfuOsPath))
                {
                    if (!File.Exists(dfuOsPath))
                    {
                        Console.WriteLine($"Cannot find {dfuOsPath} file.");
                        return;
                    }
                }
                else if (!File.Exists($"{Environment.CurrentDirectory}\\{os}"))
                {
                    Console.WriteLine($"Cannot find {os} file.");
                    return;
                }

                if (!string.IsNullOrEmpty(dfuUserPath))
                {
                    if (!File.Exists(dfuUserPath))
                    {
                        Console.WriteLine($"Cannot find {dfuUserPath} file.");
                        return;
                    }
                }
                else if (!File.Exists($"{Environment.CurrentDirectory}\\{user}"))
                {
                    Console.WriteLine($"Cannot find {user} file.");
                    return;
                }

                devices[0].Uploading += Program_Uploading;

                Upload(devices[0], $"{dfuOsPath ?? Environment.CurrentDirectory + "\\" + os}", os_address);
                Upload(devices[0], $"{dfuUserPath ?? Environment.CurrentDirectory + "\\" + user}", user_address);
            }
        }

        private static void Upload(DfuDevice device, string path, int address)
        {
            FileInfo fi = new FileInfo(path);
            byte[] bytes = File.ReadAllBytes(path);
            totalBytes = bytes.Length;

            Console.WriteLine($"Uploading {fi.Name}");
            uploadedByteCount = 0;
            device.Upload(bytes, (int)address);
            Console.WriteLine("\rdone                    ");
        }

        private static void Program_Uploading(object sender, UploadingEventArgs e)
        {
            uploadedByteCount += e.BytesUploaded;

            Console.Write($"\r{(uploadedByteCount * 100 / totalBytes)}%");
        }
    }
}
