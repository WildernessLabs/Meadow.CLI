using LibUsbDotNet.LibUsb;

namespace Meadow.LibUsb;

public class LibUsbProvider : ILibUsbProvider
{
    private const int _osAddress = 0x08000000;
    //    public const string UsbStmName = "STM32  BOOTLOADER";
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
        if (_devices == null)
        {
            _devices = _context
               .List()
               .Where(d => d.Info.VendorId == UsbBootLoaderVendorID)
               .Select(d => new LibUsbDevice(d))
               .ToList<ILibUsbDevice>();
        }

        return _devices;
    }

    public class LibUsbDevice : ILibUsbDevice
    {
        private IUsbDevice _device;

        public LibUsbDevice(IUsbDevice usbDevice)
        {
            _device = usbDevice;
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
    }
}