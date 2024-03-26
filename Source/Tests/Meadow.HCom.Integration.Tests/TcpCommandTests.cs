using Meadow.Hcom;

namespace Meadow.HCom.Integration.Tests
{
    public class TcpCommandTests
    {
        public string ValidPortName { get; } = "http://172.26.8.20:5000";

        [Fact]
        public async void TestGetDeviceInfo()
        {
            using (var connection = new TcpConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var info = await connection.GetDeviceInfo();

                Assert.NotNull(info);
            }
        }

    }
}