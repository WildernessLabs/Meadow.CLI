using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Hcom;
using Meadow.LibUsb;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MeadowCLI;

public class FirmwareWriter
{
    public IEnumerable<ILibUsbDevice> GetLibUsbDevices(bool useLegacyLibUsb = false)
    {
        ILibUsbProvider provider;

        if (useLegacyLibUsb)
        {
            provider = new ClassicLibUsbProvider();
        }
        else
        {
            provider = new LibUsbProvider();
        }

        return provider.GetDevicesInBootloaderMode();
    }

    public bool IsDfuDeviceAvailable(bool useLegacyLibUsb = false)
    {
        try
        {
            return GetLibUsbDevices(useLegacyLibUsb).Count() > 0;
        }
        catch
        {
            return false;
        }
    }

    public Task WriteOsWithDfu(string osFile, ILogger? logger = null, bool useLegacyLibUsb = false)
    {
        var devices = GetLibUsbDevices(useLegacyLibUsb);

        switch (devices.Count())
        {
            case 0: throw new Exception("No device found in bootloader mode");
            case 1: break;
            default: throw new Exception("Multiple devices found in bootloader mode - only connect one device");
        }

        var serialNumber = devices.First().GetDeviceSerialNumber();

        Debug.WriteLine($"DFU Writing file {osFile}");

        return DfuUtils.FlashFile(
        osFile,
        serialNumber,
        logger: logger,
        format: DfuUtils.DfuFlashFormat.ConsoleOut);
    }

    public Task WriteRuntimeWithHcom(IMeadowConnection connection, string firmwareFile, ILogger? logger = null)
    {
        if (connection.Device == null) throw new Exception("No connected device");

        return connection.Device.WriteRuntime(firmwareFile);
    }

    public Task WriteCoprocessorFilesWithHcom(IMeadowConnection connection, string[] files, ILogger? logger = null)
    {
        if (connection.Device == null) throw new Exception("No connected device");

        return connection.Device.WriteCoprocessorFiles(files);
    }
}