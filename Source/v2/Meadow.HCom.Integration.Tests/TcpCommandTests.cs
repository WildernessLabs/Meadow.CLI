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

                var listener = new TestListener();
                connection.AddListener(listener);

                var command = RequestBuilder.Build<GetDeviceInfoRequest>();
                command.SequenceNumber = 0;

                // dev note: something has to happen to generate messages - right now a manual reset is the action
                // in the future, we'll implement a Reset() command

                connection.EnqueueRequest(command);

                var timeoutSecs = 10;

                while (timeoutSecs-- > 0)
                {
                    if (listener.DeviceInfo.Count > 0)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }

                Assert.True(listener.DeviceInfo.Count > 0);
            }
        }

    }
}