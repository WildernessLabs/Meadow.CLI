using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.Dfu;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Meadow.CLI.Test
{

    [TestFixture]
    public class ConnectionTests
    {
        string port = "COM19";
        public readonly string osFilename = "Meadow.OS.bin";
        public readonly string runtimeFilename = "Meadow.OS.Runtime.bin";
        public readonly string networkBootloaderFilename = "bootloader.bin";
        public readonly string networkMeadowCommsFilename = "MeadowComms.bin";
        public readonly string networkPartitionTableFilename = "partition-table.bin";
        DirectoryInfo fixturesPath = new DirectoryInfo("Fixtures");

        [RequiresDevice]
        public async Task FlashLatestOsTest()
        {
            // Make sure the device is in bootloader mode
            using var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(port, logger: NullLogger.Instance);
            Assert.IsNotNull(meadow, "Initial connection");

            var flashed = await DfuUtils.FlashLatest(logger: NullLogger.Instance);
            Assert.True(flashed, "Device Flashed");
        }

        [RequiresDevice]
        public async Task AtLeastOneDevicesAttachedTest()
        {
            var ports = await MeadowDeviceManager.GetSerialPorts();
            Assert.True(ports.Count > 0, "Devices Found");
        }

        [RequiresDevice]
        public async Task MonoDisableTest()
        {
            var ports = await MeadowDeviceManager.GetSerialPorts();
            Assert.True(ports.Count > 0, "Devices Found");

            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(ports[0], logger: NullLogger.Instance);
            Assert.IsNotNull(meadow, "Initial connection");

            var cts = new CancellationTokenSource();
            await meadow.MonoDisable(cts.Token);
            await Task.Delay(2000);

            var monoEnabled = await meadow.GetMonoRunState(cts.Token);
            Assert.False(monoEnabled, "monoEnabled");
        }

        [RequiresDevice]
        public async Task MonoEnableTest()
        {
            var ports = await MeadowDeviceManager.GetSerialPorts();
            Assert.True(ports.Count > 0, "Devices Found");

            var meadow = await MeadowDeviceManager.GetMeadowForSerialPort(ports[0], logger: NullLogger.Instance);
            Assert.IsNotNull(meadow, "Initial connection");

            var cts = new CancellationTokenSource();
            await meadow.MonoEnable(cts.Token);
            await Task.Delay(2000);

            var monoEnabled = await meadow.GetMonoRunState(cts.Token);
            Assert.True(monoEnabled, "monoEnabled");
        }
    }
}