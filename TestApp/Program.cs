using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;

namespace TestApp
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello Meadow!");

            TestFlashCommands().Wait();

            //TestNonFlashDeviceManagerCommands().Wait();

            //TestFileManagerCommands().Wait();

            Console.WriteLine("el fin!");

            Console.ReadKey();
        }

        static async Task TestFileManagerCommands()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort("COM3");

            // request rejected; unknown command
            //Console.WriteLine("CreateFileSystem");
            //await MeadowFileManager.CreateFileSystem(meadow);

            //Console.WriteLine("WriteFileToFlash");
            //await MeadowFileManager.WriteFileToFlash(meadow, @"C:\Users\brikim\AppData\Local\WildernessLabs\Firmware\hello_meadow.txt");

            // MonoUpdateRt, PartitionFileSystem, MountFileSystem, InitializeFileSystem, CreateFileSystem, FormatFileSystem - do we still need these?

            //Console.WriteLine("ListFiles");
            //await MeadowFileManager.ListFiles(meadow);

            //Console.WriteLine("ListFilesAndCrcs");
            //await MeadowFileManager.ListFilesAndCrcs(meadow);

            //Console.WriteLine("DeleteFile");
            //await MeadowFileManager.DeleteFile(meadow, @"hello_meadow.txt");

            //Console.WriteLine("ListFiles");
            //await MeadowFileManager.ListFiles(meadow);

            //await MeadowFileManager.EraseFlash(meadow);

            //await MeadowFileManager.VerifyErasedFlash(meadow);
        }

        static async Task TestNonFlashDeviceManagerCommands()
        {
            try
            {
                var meadow = await MeadowDeviceManager.GetMeadowForSerialPort("COM3");

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
            }
            catch (MeadowDeviceManagerException ex)
            {
                Console.WriteLine(ex.HcomMeadowRequestType);
            }
            

        }


        static async Task TestFlashCommands()
        {
            Stopwatch sw = new Stopwatch();

            string networkBootloaderFilename = "bootloader.bin";
            string networkMeadowCommsFilename = "MeadowComms.bin";
            string networkPartitionTableFilename = "partition-table.bin";

            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort("COM3");

            //MeadowSerialDevice meadow = null;

            for(int i=0; i< 20; i++)
            {
                //meadow = await MeadowDeviceManager.GetMeadowForSerialPort("COM3");

                Console.WriteLine("disable");
                await MeadowDeviceManager.MonoDisable(meadow);

                Thread.Sleep(11000);
                //meadow = await MeadowDeviceManager.GetMeadowForSerialPort("COM3");

                Console.WriteLine("enable");
                await MeadowDeviceManager.MonoEnable(meadow);

                Thread.Sleep(11000);
            }
            

            ////await MeadowFileManager.MonoUpdateRt(meadow, @"C:\Users\brikim\AppData\Local\WildernessLabs\Firmware\Meadow.OS.Runtime.bin");
            //Console.WriteLine("write runtime");
            //await MeadowFileManager.WriteFileToFlash(meadow, @"C:\Users\brikim\AppData\Local\WildernessLabs\Firmware\Meadow.OS.Runtime.bin");
            //Console.WriteLine("mono flash");

            //sw.Start();
            //await MeadowDeviceManager.MonoFlash(meadow); // does not emit `Concluded`
            //Console.WriteLine($"elapsed: {sw.Elapsed.TotalSeconds}");

            //Console.WriteLine($"flash esp: {networkBootloaderFilename}");
            //await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice, Path.Combine(@"C:\Users\brikim\AppData\Local\WildernessLabs\Firmware", networkBootloaderFilename), mcuDestAddr: "0x1000");
            //Console.WriteLine($"flash esp: {networkPartitionTableFilename}");
            //await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice, Path.Combine(@"C:\Users\brikim\AppData\Local\WildernessLabs\Firmware", networkPartitionTableFilename), mcuDestAddr: "0x8000");
            //Console.WriteLine($"flash esp: {networkMeadowCommsFilename}");
            //await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice, Path.Combine(@"C:\Users\brikim\AppData\Local\WildernessLabs\Firmware", networkMeadowCommsFilename), mcuDestAddr: "0x10000");
        }
    }
}
