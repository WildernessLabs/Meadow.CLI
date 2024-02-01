using Meadow.Hcom;

namespace Meadow.HCom.Integration.Tests
{
    public class SerialCommandTests
    {
        public string ValidPortName { get; } = "COM10";

        [Fact]
        public async void TestDeviceReset()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                await connection.ResetDevice();

                // TODO: find a way to verify device reset
            }
        }

        [Fact]
        public async void TestGetDeviceInfo()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var info = await connection.GetDeviceInfo();

                Assert.NotNull(info);
            }
        }

        [Fact]
        public async void TestGetFileListNoCrc()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var files = await connection.GetFileList("/meadow0/", false);

                Assert.NotNull(files);
                Assert.True(files.Length > 0);
            }
        }

        [Fact]
        public async void TestGetFileListWithCrc()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var files = await connection.GetFileList("/meadow0/", true);

                Assert.NotNull(files);
                Assert.True(files.Length > 0);
            }
        }
    }
}