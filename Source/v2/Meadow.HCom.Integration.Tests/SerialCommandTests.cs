using Meadow.Hcom;

namespace Meadow.HCom.Integration.Tests
{
    public class SerialCommandTests
    {
        public string ValidPortName { get; } = "COM9";

        [Fact]
        public async void TestDeviceReset()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var listener = new TestListener();
                connection.AddListener(listener);

                var command = RequestBuilder.Build<ResetDeviceRequest>();
                command.SequenceNumber = 0;

                // dev note: something has to happen to generate messages - right now a manual reset is the action
                // in the future, we'll implement a Reset() command

                ((IMeadowConnection)connection).EnqueueRequest(command);

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
        public async void TestGetDeviceInfo()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var listener = new TestListener();
                connection.AddListener(listener);

                var command = RequestBuilder.Build<GetDeviceInfoRequest>();
                command.SequenceNumber = 0;

                // dev note: something has to happen to generate messages - right now a manual reset is the action
                // in the future, we'll implement a Reset() command

                ((IMeadowConnection)connection).EnqueueRequest(command);

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

        [Fact]
        public async void TestGetFileListNoCrc()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var listener = new TestListener();
                connection.AddListener(listener);

                var command = RequestBuilder.Build<GetFileListRequest>();
                command.SequenceNumber = 0;

                // dev note: something has to happen to generate messages - right now a manual reset is the action
                // in the future, we'll implement a Reset() command

                ((IMeadowConnection)connection).EnqueueRequest(command);

                var timeoutSecs = 10;

                while (timeoutSecs-- > 0)
                {
                    if (listener.DeviceInfo.Count > 0)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }

                Assert.True(listener.TextList.Count > 0);
            }
        }

        [Fact]
        public async void TestGetFileListWithCrc()
        {
            using (var connection = new SerialConnection(ValidPortName))
            {
                Assert.Equal(ConnectionState.Disconnected, connection.State);

                var listener = new TestListener();
                connection.AddListener(listener);

                var command = RequestBuilder.Build<GetFileListRequest>();
                command.IncludeCrcs = true;

                command.SequenceNumber = 0;

                // dev note: something has to happen to generate messages - right now a manual reset is the action
                // in the future, we'll implement a Reset() command

                ((IMeadowConnection)connection).EnqueueRequest(command);

                var timeoutSecs = 10;

                while (timeoutSecs-- > 0)
                {
                    if (listener.DeviceInfo.Count > 0)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }

                Assert.True(listener.TextList.Count > 0);
            }
        }
    }
}