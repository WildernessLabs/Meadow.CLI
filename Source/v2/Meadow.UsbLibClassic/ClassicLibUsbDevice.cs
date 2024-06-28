using LibUsbDotNet;
using LibUsbDotNet.Main;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meadow.LibUsb;

public class ClassicLibUsbProvider : ILibUsbProvider
{
    private const string UsbStmName = "STM32  BOOTLOADER";
    private const int UsbBootLoaderVendorID = 1155;
    private const int UsbMeadowVendorID = 11882;

    private string propName = (Environment.OSVersion.Platform == PlatformID.Win32NT)
            ? "FriendlyName"
            : "DeviceDesc";

    public List<ILibUsbDevice> GetDevicesInBootloaderMode()
    {
        return UsbDevice
            .AllDevices
            .Where(d => d.DeviceProperties[propName].ToString() == UsbStmName)
            .Select(d => new ClassicLibUsbDevice(d))
            .ToList<ILibUsbDevice>();
    }

    public class ClassicLibUsbDevice : ILibUsbDevice
    {
        private readonly UsbRegistry _device;
        private string? serialNumber;

        public string? SerialNumber => serialNumber;

        public ClassicLibUsbDevice(UsbRegistry usbDevice)
        {
            _device = usbDevice;

            _device.Device.Open();
            if (_device.Device.IsOpen)
            {
                if (_device.DeviceProperties != null)
                {
                    switch (Environment.OSVersion.Platform)
                    {
                        case PlatformID.Win32NT:
                            var deviceID = _device.DeviceProperties["DeviceID"].ToString();
                            if (!string.IsNullOrWhiteSpace(deviceID))
                            {
                                serialNumber = deviceID.Substring(deviceID.LastIndexOf("\\") + 1);
                            }
                            else
                            {
                                serialNumber = string.Empty;
                            }
                            break;
                        default:
                            serialNumber = _device.DeviceProperties["SerialNumber"].ToString() ?? string.Empty;
                            break;
                    }
                    _device.Device.Close();
                }
            }
        }

        public void Dispose()
        {
            _device.Device.Close();
        }

        public bool IsMeadow()
        {
            if (SerialNumber?.Length > 12)
            {
                return false;
            }
            return true;
        }
    }
}