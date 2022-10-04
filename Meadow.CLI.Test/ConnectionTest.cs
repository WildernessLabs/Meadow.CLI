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
        string port = "COM19";
        public readonly string osFilename = "Meadow.OS.bin";
        public readonly string runtimeFilename = "Meadow.OS.Runtime.bin";
        public readonly string networkBootloaderFilename = "bootloader.bin";
        public readonly string networkMeadowCommsFilename = "MeadowComms.bin";
        public readonly string networkPartitionTableFilename = "partition-table.bin";
        DirectoryInfo fixturesPath = new DirectoryInfo("Fixtures");

        // All tests are run expecting the device to already be DFU flash with OS.

        [Test]
        public async Task FlashOsTest()
        {
            var cts = new CancellationTokenSource();
            var deviceManager = new MeadowDeviceManager();
            //await deviceManager.FlashOs(port, string.Empty, string.Empty, cancellationToken: cts.Token);
        }

        [Test]
        public async Task MonoDisableTest()
        {
            var cts = new CancellationTokenSource();
            using var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port, logger: NullLogger.Instance);
            Assert.IsNotNull(meadow, "Initial connection");
            
            await meadow.MonoDisable(cts.Token);
            var monoEnabled = await meadow.GetMonoRunState(cts.Token);
            Assert.False(monoEnabled, "monoEnabled");
        }

        [Test]
        public async Task MonoEnableTest()
        {
            var cts = new CancellationTokenSource();
            using var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port, logger: NullLogger.Instance);
            Assert.IsNotNull(meadow, "Initial connection");
            
            await meadow.MonoEnable(cts.Token);
            var monoEnabled = await meadow.GetMonoRunState(cts.Token);
            Assert.True(monoEnabled, "monoEnabled");
        }
    }
}
