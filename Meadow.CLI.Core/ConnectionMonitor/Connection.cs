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

            //Serial is the best way of chcking a matching device, but meny devices don't set it
            //The meadow is no exception at this time. But worth checking.
            if (!String.IsNullOrEmpty(this.SerialNumber))
            {
                //Need to make sure the serial is not Zero, which is often an default
                if (!int.TryParse(this.SerialNumber, out int test) || test > 0)
                {
                    //Return the result either way.
                    return (this.SerialNumber == connection?.SerialNumber);
                }
            }

            //Lets try and make an educated guess
            if (this.USB != null && connection.USB != null)
            {
                //Bus numbers should be the same
                if (this.USB.BusNumber == connection.USB.BusNumber)
                {
                   //Device numbers (at least on Linux) incroment on each USB connection.
                   //Test to see if tha same, just in case.
                   if (this.USB.DeviceNumber == connection.USB.DeviceNumber) return true;
                   //If the new device has a higher device number, than a previous recent disconnect
                   if (this.Removed && this.USB.DeviceNumber < connection.USB.DeviceNumber
                       && (connection.TimeConnected - this.TimeRemoved).TotalSeconds < 2 )
                   {
                        return true;
                   }
                }
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
