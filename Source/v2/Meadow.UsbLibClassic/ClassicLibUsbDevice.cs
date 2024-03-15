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

    public List<ILibUsbDevice> GetDevicesInBootloaderMode()
    {
        var propName = (Environment.OSVersion.Platform == PlatformID.Win32NT)
            ? "FriendlyName"
            : "DeviceDesc";

        return UsbDevice
            .AllDevices
            .Where(d => d.DeviceProperties[propName].ToString() == UsbStmName)
            .Select(d => new ClassicLibUsbDevice(d))
            .Cast<ILibUsbDevice>()
            .ToList();
    }
}

public class ClassicLibUsbDevice : ILibUsbDevice
{
    private readonly UsbRegistry _device;

    public ClassicLibUsbDevice(UsbRegistry usbDevice)
    {
        _device = usbDevice;
    }

    public void Dispose()
    {
        _device.Device.Close();
    }

    public string GetDeviceSerialNumber()
    {
        if (_device != null && _device.DeviceProperties != null)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    var deviceID = _device.DeviceProperties["DeviceID"].ToString();
                    if (!string.IsNullOrWhiteSpace(deviceID))
                    {
                        return deviceID.Substring(deviceID.LastIndexOf("\\") + 1);
                    }
                    else
                    {
                        return string.Empty;
                    }
                default:
                    return _device.DeviceProperties["SerialNumber"].ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    public bool IsMeadow()
    {
        if (_device.Vid != 1155)
        {
            return false;
        }
        if (GetDeviceSerialNumber().Length > 12)
        {
            return false;
        }
        return true;
    }
}