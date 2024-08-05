using LibUsbDotNet.LibUsb;
using System.Collections.Generic;
using System.Linq;

namespace Meadow.LibUsb;

public class LibUsbProvider : ILibUsbProvider
{
    private const int UsbBootLoaderVendorID = 1155;
    private const int UsbMeadowVendorID = 11882;

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
        private string? serialNumber;

        public string? SerialNumber => serialNumber;

        public LibUsbDevice(IUsbDevice usbDevice)
        {
            _device = usbDevice;

            _device.Open();
            if (_device.IsOpen)
            {
                serialNumber = _device.Info?.SerialNumber ?? string.Empty;
                _device.Close();
            }
        }

        public void Dispose()
        {
            _device?.Dispose();
        }

        public bool IsMeadow()
        {
            if (serialNumber?.Length > 12)
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