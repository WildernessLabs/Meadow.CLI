using System;
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

            GetFilesAndCrcs(); //fire and forget 

            Thread.Sleep(200000);
        }

        static async Task GetFilesAndCrcs()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort("/dev/tty.usbmodem01");

            // MeadowDeviceManager.MonoDisable(meadow);

            await meadow.SetDeviceInfo();

        //    var (files, crcs) = await meadow.GetFilesAndCrcs();

      //      var fileList = await meadow.GetFilesOnDevice();
      //      Console.WriteLine("File list received");
        }
    }
}
