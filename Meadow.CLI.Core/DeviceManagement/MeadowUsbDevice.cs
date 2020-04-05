using System;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace Meadow.CLI.DeviceManagement
{
    public class MeadowUsbDevice
    {
        public enum eDeviceType
        {
            MeadowBoot,
            MeadowMono
        }

        public eDeviceType DeviceType { get; set; }
   
        public string Serial { get; set; }

        public string UsbDeviceName { get; set; }
        public string Port { get; internal set; }
        public ushort VendorID { get; internal set; }
        public ushort ProductID { get; internal set; }
        public string ManufacturerString { get; internal set; }

        public override string ToString()
        {
            return UsbDeviceName + "::" + Serial;
        }
    }
}
