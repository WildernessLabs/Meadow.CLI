using Meadow.Hcom;

namespace Meadow.HCom.Integration.Tests
{
    public class TcpConnectionTests
    {
        public string ValidPortName { get; } = "http://172.26.8.20:5000";

        [Fact]
        public async void TestAttachPositive()
        {
            using (var connection = new TcpConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);
                var connected = await connection.Attach(null, 20);
                Assert.Equal(ConnectionState.Connected, connection.State);

                while (true)
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}