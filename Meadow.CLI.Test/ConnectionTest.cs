using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging.Abstractions;
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
            var cts = new CancellationTokenSource();
            //DfuUpload.FlashOS(Path.Combine(fixturesPath.FullName, osFilename));
            var deviceManager = new MeadowDeviceManager(NullLoggerFactory.Instance);
            using (var meadow = deviceManager.GetMeadowForSerialPort(port))
            {
                Assert.IsNotNull(meadow, "Initial connection");
                await meadow.MonoDisableAsync(cts.Token);
                var isEnabled = await meadow.GetMonoRunStateAsync(cts.Token);
                // try to disable one more time
                if (isEnabled)
                {
                    await meadow.MonoDisableAsync(cts.Token);
                    isEnabled = await meadow.GetMonoRunStateAsync(cts.Token);
                }
                Assert.IsFalse(isEnabled, "Disable mono");
            }

            using (var meadow = deviceManager.GetMeadowForSerialPort(port))
            {
                await meadow.UpdateMonoRuntimeAsync(Path.Combine(fixturesPath.FullName, runtimeFilename), cancellationToken: cts.Token);
            }

            using (var meadow = deviceManager.GetMeadowForSerialPort(port))
            {
                await meadow.FlashEspAsync(fixturesPath.FullName, cts.Token);
            }

            using (var meadow = deviceManager.GetMeadowForSerialPort(port))
            {
                Assert.IsNotNull(meadow);
                await meadow.MonoEnableAsync(cts.Token);
                var isEnabled = await meadow.GetMonoRunStateAsync(cts.Token);
                Assert.IsTrue(isEnabled);
            }
        }
    }
}
