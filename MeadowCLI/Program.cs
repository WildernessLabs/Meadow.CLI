using CommandLine;
using CommandLine.Text;
using DfuSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MeadowCLI
{
    class Program
    {
        static string os = "nuttx.bin";
        static string user = "nuttx_user.bin";

        static int os_address = 0x08000000;
        static int user_address = 0x08040000;

        static int uploadedByteCount = 0;
        static int totalBytes = 0;

        public class Options
        {
            [Option('d', "dfu", Required = false, HelpText = "DFU copy os and user files. Looks for files in execution direction. To override, user 'osFile' and 'userFile'.")]
            public bool Dfu { get; set; }
            [Option(longName: "osFile", Default = null, Required = false, HelpText = "File path to os file. Usage: --osFile mypath")]
            public string DfuOsPath { get; set; }
            [Option(longName: "userFile", Default = null, Required = false, HelpText = "File path to user file. Usage: --userFile mypath")]
            public string DfuUserPath { get; set; }

        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new string[] { "--help" };
            }
            CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(opts =>
            {
                if (opts.Dfu)
                {
                    DfuContext.Init();
                    var devices = DfuContext.Current.GetDevices();

                    if (devices.Count < 1)
                    {
                        Console.WriteLine("Attach a device in DFU mode, mofo.");
                    }
                    else
                    {
                        

                        if (!string.IsNullOrEmpty(opts.DfuOsPath))
                        {
                            if (!File.Exists(opts.DfuOsPath))
                            {
                                Console.WriteLine($"Cannot find {opts.DfuOsPath} file.");
                                return;
                            }
                        }
                        else if (!File.Exists($"{Environment.CurrentDirectory}\\{os}"))
                        {
                            Console.WriteLine($"Cannot find {os} file.");
                            return;
                        }

                        if (!string.IsNullOrEmpty(opts.DfuUserPath) )
                        {
                            if (!File.Exists(opts.DfuUserPath))
                            {
                                Console.WriteLine($"Cannot find {opts.DfuUserPath} file.");
                                return;
                            }
                        }
                        else if (!File.Exists($"{Environment.CurrentDirectory}\\{user}"))
                        {
                            Console.WriteLine($"Cannot find {user} file.");
                            return;
                        }

                        devices[0].Uploading += Program_Uploading;

                        Upload(devices[0], $"{opts.DfuOsPath ?? Environment.CurrentDirectory + "\\" + os}", os_address);
                        Upload(devices[0], $"{opts.DfuUserPath ?? Environment.CurrentDirectory + "\\" + user}", user_address);
                    }
                }
            });

            Console.ReadKey();
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

        private static void Program_Uploading(object sender, DfuSharp.UploadingEventArgs e)
        {
            uploadedByteCount += e.BytesUploaded;

            Console.Write($"\r{(uploadedByteCount * 100 / totalBytes)}%");
        }
    }
}
