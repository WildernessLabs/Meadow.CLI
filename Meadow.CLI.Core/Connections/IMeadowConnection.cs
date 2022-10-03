using Meadow.CLI.Core.Devices;

namespace Meadow.CLI.Core
{
    public delegate void ConnectionStateHandler(IMeadowConnection connection, bool newState);
    public delegate void ConnectionPresenseHander(IMeadowConnection connection);

    public interface IMeadowConnection
    {
        public event ConnectionStateHandler ConnectionStateChanged;

        public string Name { get; }
        public IMeadowDevice? Device { get; }
        public bool IsConnected { get; }
        public bool AutoReconnect { get; set; }

        public void Connect();
        public void Disconnect();
    }
}
