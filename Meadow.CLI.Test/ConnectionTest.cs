using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;
using MeadowCLI.Hcom;
using NUnit.Framework;

namespace Meadow.CLI.Test
{
    [TestFixture]
    public class ConnectionTest
    {
        string port = "COM3";
        public readonly string osFilename = "Meadow.OS.bin";
        public readonly string runtimeFilename = "Meadow.OS.Runtime.bin";
        public readonly string networkBootloaderFilename = "bootloader.bin";
        public readonly string networkMeadowCommsFilename = "MeadowComms.bin";
        public readonly string networkPartitionTableFilename = "partition-table.bin";
        DirectoryInfo fixturesPath = new DirectoryInfo("../../../../Fixtures");

        // All tests are run expecting the device to already be DFU flash with OS.

        [Test]
        public async Task BasicConnectionTest()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            Assert.IsNotNull(meadow);
        }

        [Test]
        public async Task ReconnectTest()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            await MeadowDeviceManager.ResetMeadow(meadow);
            meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            Assert.IsNotNull(meadow);
        }

        [Test]
        public async Task MonoDisableTest()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            Assert.IsNotNull(meadow);
            await MeadowDeviceManager.MonoDisable(meadow);
        }

        [Test]
        public async Task MonoEnableTest()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            Assert.IsNotNull(meadow);
            await MeadowDeviceManager.MonoEnable(meadow);
        }

        [Test]
        public async Task RuntimeFlashTest()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            meadow.OnMeadowMessage += MeadowMesageHandler;
            await MeadowDeviceManager.MonoDisable(meadow);
            await MeadowFileManager.WriteFileToFlash(meadow, Path.Combine(fixturesPath.FullName, runtimeFilename));
            await MeadowDeviceManager.MonoFlash(meadow);
            await MeadowDeviceManager.MonoEnable(meadow);
            meadow.OnMeadowMessage -= MeadowMesageHandler;
        }

        [Test]
        public async Task EspFlashTest()
        {
            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
            meadow.OnMeadowMessage += MeadowMesageHandler;
            await MeadowDeviceManager.MonoDisable(meadow);
            await MeadowFileManager.WriteFileToEspFlash(meadow, Path.Combine(fixturesPath.FullName, networkMeadowCommsFilename), mcuDestAddr: "0x10000");
            await MeadowFileManager.WriteFileToEspFlash(meadow, Path.Combine(fixturesPath.FullName, networkBootloaderFilename), mcuDestAddr: "0x1000");
            await MeadowFileManager.WriteFileToEspFlash(meadow, Path.Combine(fixturesPath.FullName, networkPartitionTableFilename), mcuDestAddr: "0x8000");
            await MeadowDeviceManager.MonoEnable(meadow);
            meadow.OnMeadowMessage -= MeadowMesageHandler;
        }

        [Test]
        public async Task ReconnectLoadTest()
        {
            MeadowSerialDevice meadow;
            for(int i=0; i<10; i++)
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port);
                Assert.IsNotNull(meadow);
                await MeadowDeviceManager.MonoEnable(meadow);
            }
        }
        public void MeadowMesageHandler(object sender, MeadowMessageEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Message))
            {
                Debug.WriteLine(e.Message);
            }
        }
    }
}
