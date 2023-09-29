using LibUsbDotNet.LibUsb;

namespace Meadow.LibUsb;

public class UsbLibProvider : ILibUsbProvider
{
    private const int _osAddress = 0x08000000;
    //    public const string UsbStmName = "STM32  BOOTLOADER";
    private const int UsbBootLoaderVendorID = 1155;

    public List<ILibUsbDevice> GetDevicesInBootloaderMode()
    {
        using UsbContext context = new UsbContext();

        return context
            .List()
            .Where(d => d.Info.VendorId == UsbBootLoaderVendorID)
            .Select(d => new LibUsbDevice(d))
            .Cast<ILibUsbDevice>()
            .ToList();
    }
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
