using System;
using System.Collections.Generic;

namespace Meadow.LibUsb;

public interface ILibUsbProvider
{
    List<ILibUsbDevice> GetDevicesInBootloaderMode();
}

public interface ILibUsbDevice : IDisposable
{
    string GetDeviceSerialNumber();

    bool IsMeadow();
}