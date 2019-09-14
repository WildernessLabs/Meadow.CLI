using System;
namespace Meadow.CLI.DeviceManagement
{
    public class MeadowUsbDevice
    {
        // or some shit
        public string Serial { get; set; }
        //
        public string UsbDeviceName { get; set; }

        public override string ToString()
        {
            return UsbDeviceName + "::" + Serial;
        }
    }
}
