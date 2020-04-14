using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meadow.CLI.DeviceMonitor
{
    public class UdevTTYMonitor : IDeviceMonitor, IDisposable
    {
        readonly string[] PortPaths = { "ttyAMA", "ttyUSB" };
        FileSystemWatcher watcher;
        List<IDevice> DeviceList = new List<IDevice>();
        
        public event EventHandler<IDevice> DeviceNew;
        public event EventHandler<IDevice> DeviceRemoved;

        readonly object lockObject = new object();
    
        public UdevTTYMonitor()
        {
            watcher = new FileSystemWatcher()
            {
                Path = "/dev",
                Filter = "tty*",
                IncludeSubdirectories = false,
            };
            
            watcher.Created += Watcher_Created;
            watcher.Deleted += Watcher_Deleted;

            PreLoad();
            
            watcher.EnableRaisingEvents = true;
        }


        void PreLoad()
        {
            foreach (var file in PortPaths)
            {
                lock (lockObject)
                {
                    foreach (var portname in Directory.GetFiles("/dev", $"{file}*", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                    {
                        var device = new IDevice(portname);
                        if (IsTargetDevice(device)) DeviceList.Add(portname, device);
                    }
                }
            }
        }


        bool IsTargetDevice(IDevice device)
        {
            return (device.VendorID == 0x2e6a && device.ProductID == 0x0);
        }

        void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            lock (lockObject)
            {
                var device = new DeviceUdv();
                if (IsTargetDevice(device))
                {
                    DeviceList.Add(device);
                    DeviceNew?.Invoke(this, device);
                }
            }
        }

        void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            lock (lockObject)
            {
                UdevTTYDevice device;
                if (DeviceList.TryGetValue(e.FullPath, out device))
                {
                    DeviceRemoved?.Invoke(this, device);
                    DeviceList.Remove(e.FullPath);
                }
            }
        }




        public void Dispose()
        {
            watcher.Created -= Watcher_Created;
            watcher.Deleted -= Watcher_Deleted;
            watcher.Dispose();
        }
    }
}
