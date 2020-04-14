using System;
using System.Collections.Generic;

namespace Meadow.CLI.DeviceMonitor
{

    public interface IConnectionMonitor : IDisposable
    {
        event EventHandler<Connection> DeviceNew;
        event EventHandler<Connection> DeviceRemoved;
        List<Connection> GetDeviceList();

        void Dispose();    
    }
}
