using System;
using System.Net;

namespace Meadow.CLI.DeviceMonitor
{

    public enum MeadowMode
    {
        MeadowBoot,
        MeadowMono
    }

    public class MeadowDevice
    {
        public event EventHandler RemovedEvent;
        
        public MeadowMode Mode { get; set; }
                
        public string SerialNumber { get; set; }
        public USB_interface USB  { get; set; }
        public IP_interface IP  { get; set; }

        bool _removed;
        public bool Removed {
            get
            {
                return _removed;
            }
            set
            {
                if (!_removed)
                {
                    _removed = value;
                    RemovedEvent?.Invoke(this, null);
                }
            }
        }

        public class USB_interface
        {
            public string DevicePort { get; set; }
            public ushort BusNumber { get; set ; }
            public ushort DeviceNumber { get; set; }
            public ushort VendorID { get; set; }
            public ushort ProductID { get; set; }
        }

        public class IP_interface
        {
            public IPEndPoint Endpoint  { get; set; }
        }
        
    }
}
