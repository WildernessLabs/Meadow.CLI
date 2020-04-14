using System;
using System.Net;

namespace Meadow.CLI.DeviceMonitor
{

    public enum MeadowMode
    {
        MeadowBoot,
        MeadowMono
    }

    /// <summary>
    /// Connection class. Created when device is connected to USB or IP connection.
    /// </summary>
    public class Connection
    {
        public event EventHandler RemovedEvent;
        
        public MeadowMode Mode { get; set; }
                
        public string SerialNumber { get; set; }
        public USB_interface USB  { get; set; }
        public IP_interface IP  { get; set; }

        public DateTime TimeConnected { get; } = DateTime.UtcNow;
        public DateTime TimeRemoved { get; private set; } 

        bool _removed;
        /// <summary>
        /// Set to true, when USB device removed.  This class should not be reused.
        /// </summary>
        /// <value><c>true</c> if removed; otherwise, <c>false</c>.</value>
        public bool Removed {
            get
            {
                return _removed;
            }
            set
            {
                if (value && !_removed)
                {
                    TimeRemoved = DateTime.UtcNow;
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

        /// <summary>
        /// Match this connection with another.
        /// </summary>
        /// <returns><c>true</c>, if match was ised, <c>false</c> otherwise.</returns>
        /// <param name="connection class">Connection.</param>
        public bool IsMatch(Connection connection)
        {
            if (this == connection) return true;
            if (this.Mode != connection.Mode) return false;
            
            //The meadow does not have a USB serial at this time.
            //if (!String.IsNullOrEmpty(this.SerialNumber) && this.SerialNumber == connection?.SerialNumber) return true;

            if (this.USB != null && connection.USB != null)
            {
                if (this.USB.BusNumber == USB.BusNumber && this.USB.DeviceNumber == USB.DeviceNumber) return true;
            }

            if (this.IP != null && connection.IP != null)
            {
                if (this.IP.Endpoint == connection.IP.Endpoint) return true;
            }
            return false;
        }

        public override string ToString()
        {
            if (USB != null)
            {
                return $"{USB.DevicePort ?? "[no port]"} {Mode}";
            }

            if (IP != null)
            {
                return $"{(IP?.Endpoint.ToString() ?? "[no ip]")} {Mode}";
            }

            return "<unknown>";
        }
    }
}
