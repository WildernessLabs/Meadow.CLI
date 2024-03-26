using Meadow.CLI.Core.Devices;
using System;
using System.Threading.Tasks;

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
        bool MonitorState { get; set; }

        Task<bool> WaitForConnection(TimeSpan timeout);
        public void Connect();
        public void Disconnect();
    }
}
