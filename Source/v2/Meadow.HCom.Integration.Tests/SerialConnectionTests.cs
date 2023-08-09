using Meadow.Hcom;

namespace Meadow.HCom.Integration.Tests
{
    public class SerialConnectionTests
    {
        public string ValidPortName { get; } = "COM3";

        [Fact]
        public void TestInvalidPortName()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var connection = new SerialConnection("COMxx");
            });
        }

        [Fact]
        public async void TestListen()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var listener = new TestListener();
                connection.AddListener(listener);

                // dev note: something has to happen to generate messages - right now a manual reset is the action
                // in the future, we'll implement a Reset() command

                var timeoutSecs = 10;

                while (timeoutSecs-- > 0)
                {
                    if (listener.Messages.Count > 0)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }

                Assert.True(listener.Messages.Count > 0);
            }
        }

        [Fact]
        public async void TestAttachPositive()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);
                var connected = await connection.Attach(null, 2);
                Assert.Equal(ConnectionState.Connected, connection.State);

                while (true)
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}