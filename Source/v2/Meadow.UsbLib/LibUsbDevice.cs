using LibUsbDotNet.LibUsb;
using System.Collections.Generic;
using System.Linq;

namespace Meadow.LibUsb;

public class LibUsbProvider : ILibUsbProvider
{
    private const int UsbBootLoaderVendorID = 1155;

    internal static UsbContext _context;
    internal static List<ILibUsbDevice>? _devices;
    static LibUsbProvider()
    {
        // only ever create one of these - there's a bug in the LibUsbDotNet library and when this disposes, things go sideways
        _context = new UsbContext();
    }

    public List<ILibUsbDevice> GetDevicesInBootloaderMode()
    {
        _devices = _context
           .List()
           .Where(d => d.Info.VendorId == UsbBootLoaderVendorID)
           .Select(d => new LibUsbDevice(d))
           .ToList<ILibUsbDevice>();

        return _devices;
    }

    public class LibUsbDevice : ILibUsbDevice
    {
        private readonly IUsbDevice _device;

        public LibUsbDevice(IUsbDevice usbDevice)
        {
            _device = usbDevice;
        }

        public void Dispose()
        {
            _device?.Dispose();
        }

        public string GetDeviceSerialNumber()
        {
            var serialNumber = string.Empty;

            _device.Open();
            if (_device.IsOpen)
            {
                serialNumber = _device.Info?.SerialNumber ?? string.Empty;
                _device.Close();
            }

            return serialNumber;
        }

        public bool IsMeadow()
        {
            if (_device.VendorId != 1155)
            {
                return false;
            }
            if (GetDeviceSerialNumber().Length > 12)
            {
                return false;
            }
            if (_device as UsbDevice is { } usbDevice)
            {
                if (usbDevice.ActiveConfigDescriptor.Interfaces.Count != 4)
                {
                    return false;
                }
            }
            return true;
        }
    }
}