using Meadow.CLI.Core.DeviceManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public class MeadowConnectionManager : IEnumerable<IMeadowConnection>
    {
        public event ConnectionPresenseHander ConnectionAdded = delegate { };
        public event ConnectionPresenseHander ConnectionRemoved = delegate { };

        private Dictionary<string, IMeadowConnection> _connections = new Dictionary<string, IMeadowConnection>(StringComparer.InvariantCultureIgnoreCase);

        public ILogger? Logger { get; }

        public MeadowConnectionManager(ILogger? logger)
        {
            Logger = logger;
            Task.Run(ConnectionAvailabilityMonitorProc);
        }

        public IMeadowConnection? this[string name]
        {
            get => _connections.ContainsKey(name) ? _connections[name] : null;
        }

        public IMeadowConnection AddConnection(string serialPort)
        {
            if (_connections.ContainsKey(serialPort))
            {
                return _connections[serialPort];
            }

            var connection = new MeadowConnection(serialPort, Logger);
            _connections.Add(serialPort, connection);
            return connection;
        }

        private async Task ConnectionAvailabilityMonitorProc()
        {
            while (true)
            {
                var allPorts = await MeadowSerialPortManager.GetSerialPorts();

                var added = allPorts.Except(_connections.Keys);
                var removed = _connections.Keys.Except(allPorts);

                foreach (var a in added)
                {
                    var connection = AddConnection(a);
                    ConnectionAdded?.Invoke(connection);
                }

                foreach (var c in removed)
                {
                    // should we remove this?  when disconnected and reconnected, it shouldn't go away?  Maybe after some time period?
                    // ConnectionRemoved?.Invoke(_connections[c]);
                    // _connections.Remove(c);
                }

                await Task.Delay(1000);
            }
        }

        public IEnumerator<IMeadowConnection> GetEnumerator()
        {
            return _connections.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}