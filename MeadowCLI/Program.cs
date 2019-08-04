using CommandLine;
using System;
using MeadowCLI.DeviceManagement;
using Meadow.CLI.DeviceManagement;

namespace MeadowCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new string[] { "--help" };
            }
            Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(opts =>
            {
                if (opts.Dfu)
                {
                    //ToDo update to use command line args for os and user
                    DfuUpload.FlashNuttx(opts.DfuOsPath, opts.DfuUserPath);
                }
                else
				{
					ConnectToMeadowDevice(opts.SerialPort);

                    if(opts.WriteFile)
                    {
                        MeadowFileManager.WriteFileToFlash(DeviceManager.CurrentDevice,
                            opts.ExtFileName, opts.TargetFileName, opts.Partition);
                    }
                    else if(opts.DeleteFile)
                    {
                        MeadowFileManager.DeleteFile(DeviceManager.CurrentDevice,
                            opts.TargetFileName, opts.Partition);
                    }
                    else if(opts.EraseFlash)
                    {
                        MeadowFileManager.Erase(DeviceManager.CurrentDevice);
                    }
                    else if(opts.EraseFlashAndVerify)
                    {
                        MeadowFileManager.EraseAndVerify(DeviceManager.CurrentDevice);
                    }
                    else if(opts.PartitionFileSystem)
                    {
                        MeadowFileManager.PartitionFileSystem(DeviceManager.CurrentDevice, opts.NumberOfPartitions);
                    }
				}
			});

            Console.ReadKey();
        }

        //temp code until we get the device manager logic in place 
        static void ConnectToMeadowDevice (string commPort)
		{
			DeviceManager.CurrentDevice = new MeadowDevice(commPort);

		}
    }
}