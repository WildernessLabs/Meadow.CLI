using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meadow.CLI.Internals.Udev;

namespace Meadow.CLI.DeviceMonitor
{

    /// <summary>
    /// Connection monitor for Linux udev.
    /// </summary>
    public class ConnectionMonitorUdev : IConnectionMonitor, IDisposable
    {
        readonly string[] PortPaths = { "ttyACM", "ttyUSB" };
        FileSystemWatcher watcher;
        List<Connection> DeviceList = new List<Connection>();
        
        public event EventHandler<Connection> DeviceNew;
        public event EventHandler<Connection> DeviceRemoved;

        readonly object lockObject = new object();
    
        public ConnectionMonitorUdev(bool StartWatcher)
        {
            if (StartWatcher)
            {
                Console.WriteLine("ConnectionMonitorUdev: Starting");
                watcher = new FileSystemWatcher()
                {
                    Path = "/dev",
                    Filter = "tty*",
                    IncludeSubdirectories = false,
                };

                watcher.Created += Watcher_Created;
                watcher.Deleted += Watcher_Deleted;
            }
            
            PreLoad();
            
            if (StartWatcher) watcher.EnableRaisingEvents = true;
        }

        public List<Connection> GetDeviceList()
        {
            lock (lockObject)
            {
                return DeviceList.ToList();
            }
        }

        void PreLoad()
        {
            lock (lockObject)
            {
                foreach (var file in PortPaths)
                {
                    foreach (var ttyPath in Directory.GetFiles("/dev", $"{file}*", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                    {
                        var connection = Udev.GetDeviceFromPath(ttyPath);
                        var mode = IsTargetDevice(connection);
                        if (mode.HasValue)
                        {
                            connection.Mode = mode.Value;
                            DeviceList.Add(connection);
                        }
                    }
                }
                Console.WriteLine($"ConnectionMonitorUdev: Found {DeviceList.Count} devices.");
            }
        }

        /// <summary>
        /// Is this device reporting as a Meadow.
        /// </summary>
        /// <returns>The target device.</returns>
        /// <param name="device">Device.</param>
        MeadowMode? IsTargetDevice(Connection device)
        {
            if (device.USB != null)
            {
                switch (device.USB.VendorID)
                {
                    case 0x483:  // STMicro
                        if (device.USB.ProductID != 0xdf11) return null;
                        return MeadowMode.MeadowBoot;
                    case 0x2e6a: // Wilderness Labs
                        return MeadowMode.MeadowMono;
                }
            }
            return null;
        }

        void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            lock (lockObject)
            {
                var connection = Udev.GetDeviceFromPath(e.FullPath);
                var mode = IsTargetDevice(connection);
                if (mode.HasValue)
                {
                    Console.WriteLine($"ConnectionMonitorUdev: Added {e.FullPath}.");
                    connection.Mode = mode.Value;
                    DeviceList.Add(connection);
                    
                    DeviceNew?.Invoke(this, connection);
                }
            }
        }

        void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            lock (lockObject)
            {
                Connection device = DeviceList.Find(x => x.USB != null && x.USB.DevicePort == e.FullPath);
                if (device!=null)
                {
                    Console.WriteLine($"ConnectionMonitorUdev: Removed {device?.USB.DevicePort}.");
                    device.Removed = true;  //This triggers an event in MeadowDevice
                    DeviceList.Remove(device);                    
                    DeviceRemoved?.Invoke(this, device);                    
                }
            }
        }

        public void Dispose()
        {
            if (watcher != null)
            {
                watcher.Created -= Watcher_Created;
                watcher.Deleted -= Watcher_Deleted;
                watcher.Dispose();
            }
        }
    }
}
