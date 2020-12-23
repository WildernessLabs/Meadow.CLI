using System.IO;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;
using NUnit.Framework;

namespace Meadow.CLI.Test
{
    [TestFixture]
    public class ConnectionTest
    {
        string port = "COM5";
        public readonly string osFilename = "Meadow.OS.bin";
        public readonly string runtimeFilename = "Meadow.OS.Runtime.bin";
        public readonly string networkBootloaderFilename = "bootloader.bin";
        public readonly string networkMeadowCommsFilename = "MeadowComms.bin";
        public readonly string networkPartitionTableFilename = "partition-table.bin";
        DirectoryInfo fixturesPath = new DirectoryInfo("Fixtures");

        // All tests are run expecting the device to already be DFU flash with OS.

        [Test]
        public async Task FlashOSTest()
        {
            //DfuUpload.FlashOS(Path.Combine(fixturesPath.FullName, osFilename));
            
            using (var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port))
            {
                Assert.IsNotNull(meadow, "Initial connection");
                await MeadowDeviceManager.MonoDisable(meadow);
                var isEnabled = await MeadowDeviceManager.MonoRunState(meadow);
                // try to disable one more time
                if (isEnabled)
                {
                    await MeadowDeviceManager.MonoDisable(meadow);
                    isEnabled = await MeadowDeviceManager.MonoRunState(meadow);
                }
                Assert.IsFalse(isEnabled, "Disable mono");
            }

            using (var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port))
            {
                await MeadowFileManager.MonoUpdateRt(meadow, Path.Combine(fixturesPath.FullName, runtimeFilename));
            }

            using (var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port))
            {
                await MeadowFileManager.FlashEsp(meadow, fixturesPath.FullName);
            }

            using (var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port))
            {
                Assert.IsNotNull(meadow);
                await MeadowDeviceManager.MonoEnable(meadow);
                var isEnabled = await MeadowDeviceManager.MonoRunState(meadow);
                Assert.IsTrue(isEnabled);
            }
        }
    }
}
