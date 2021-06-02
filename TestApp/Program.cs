using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    class MainClass
    {
        //static string _devicePort = "/dev/tty.usbmodem3366337830361"; //mac
        static string _devicePort = "COM14"; //win
        public static void Main(string[] args)
        {
            ExecuteCLI("--FlashOS");
            ExecuteCLI("--Download");
            ExecuteCLI($"--MonoDisable -s {_devicePort}");
            // try one more time. this is usually needed right after OS flash for some reason
            ExecuteCLI($"--MonoDisable -s {_devicePort}");
            ExecuteCLI("--MonoUpdateRt");
            ExecuteCLI("--FlashEsp");
            ExecuteCLI("--MonoEnable");

            Console.WriteLine("Done!");
            Console.Read();
        }

        static void ExecuteCLI(string arg)
        {
            char pad = '=';
            Console.Write("".PadLeft(40, pad));
            Console.WriteLine($" {arg} ".PadRight(60, pad));

            using (var process = new Process())
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    process.StartInfo.FileName = @"..\..\..\..\Meadow.CLI\bin\Debug\net5.0\Meadow.CLI.exe";
                    process.StartInfo.Arguments = arg;
                }
                else
                {
                    // macos, haven't tested on linux :/
                    process.StartInfo.FileName = "dotnet";
                    process.StartInfo.Arguments = $"./Meadow.CLI/bin/Debug/net5.0/Meadow.CLI.dll {arg}";
                }

                process.StartInfo.UseShellExecute = false;
                process.Start();

                process.WaitForExit();
            }
        }

        /*

        FileManager commands to test

        // MonoUpdateRt, PartitionFileSystem, MountFileSystem, InitializeFileSystem, CreateFileSystem, FormatFileSystem - do we still need these?

        Console.WriteLine("WriteFileToFlash");
        File.WriteAllText(".\\hello_meadow.txt", "test");

        Console.WriteLine("ListFiles");
        await MeadowFileManager.ListFiles(meadow);

        Console.WriteLine("ListFilesAndCrcs");
        await MeadowFileManager.ListFilesAndCrcs(meadow);

        Console.WriteLine("DeleteFile");
        await MeadowFileManager.DeleteFile(meadow, @"hello_meadow.txt");

        Console.WriteLine("ListFiles");
        await MeadowFileManager.ListFiles(meadow);

        await MeadowFileManager.EraseFlash(meadow);
        await MeadowFileManager.VerifyErasedFlash(meadow);

        DeviceManager commands to test

        Console.WriteLine("SetTraceLevel");
        await MeadowDeviceManager.SetTraceLevel(meadow, 1);

        // does not sent Concluded
        //Console.WriteLine("ResetMeadow");
        //await MeadowDeviceManager.ResetMeadow(meadow);

        // not implemented
        //Console.WriteLine("EnterDfuMode");
        //await MeadowDeviceManager.EnterDfuMode(meadow);

        // request rejected; unknown command
        //Console.WriteLine("NshEnable");
        //await MeadowDeviceManager.NshEnable(meadow);

        Console.WriteLine("MonoRunState");
        await MeadowDeviceManager.MonoRunState(meadow);

        Console.WriteLine("GetDeviceInfo");
        await MeadowDeviceManager.GetDeviceInfo(meadow);

        Console.WriteLine("GetDeviceName");
        await MeadowDeviceManager.GetDeviceName(meadow);

        Console.WriteLine("SetDeveloper1");
        await MeadowDeviceManager.SetDeveloper1(meadow, 1);

        Console.WriteLine("SetDeveloper2");
        await MeadowDeviceManager.SetDeveloper2(meadow, 1);

        Console.WriteLine("SetDeveloper3");
        await MeadowDeviceManager.SetDeveloper3(meadow, 1);

        Console.WriteLine("SetDeveloper4");
        await MeadowDeviceManager.SetDeveloper4(meadow, 1);

        Console.WriteLine("TraceDisable");
        await MeadowDeviceManager.TraceDisable(meadow);

        Console.WriteLine("TraceEnable");
        await MeadowDeviceManager.TraceEnable(meadow);

        Console.WriteLine("Uart1Apps");
        await MeadowDeviceManager.Uart1Apps(meadow);

        Console.WriteLine("Uart1Trace");
        await MeadowDeviceManager.Uart1Trace(meadow);

        // restarts device. send reconnect?
        //Console.WriteLine("RenewFileSys");
        //await MeadowDeviceManager.RenewFileSys(meadow);

        // request rejected; unknown command
        //Console.WriteLine("QspiWrite");
        //await MeadowDeviceManager.QspiWrite(meadow, 1);

        // request rejected; unknown command
        //Console.WriteLine("QspiRead");
        //await MeadowDeviceManager.QspiRead(meadow, 1);

        //request rejected; unknown command
        //Console.WriteLine("QspiInit");
        //await MeadowDeviceManager.QspiInit(meadow, 1);

        // mono needs to be disabled for the ESP commands
        await MeadowDeviceManager.MonoDisable(meadow);

        Console.WriteLine("Esp32ReadMac");
        await MeadowDeviceManager.Esp32ReadMac(meadow);

        Console.WriteLine("Esp32Restart");
        await MeadowDeviceManager.Esp32Restart(meadow);
         */
    }
}
