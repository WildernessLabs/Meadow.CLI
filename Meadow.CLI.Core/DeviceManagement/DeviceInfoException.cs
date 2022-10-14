using System;

namespace Meadow.CLI.Core.DeviceManagement
{
    // TODO: Inherit from MeadowDeviceException
    public class DeviceInfoException : Exception
    {
        public DeviceInfoException(Exception? innerException = null) : 
            base("An exception occurred while retrieving the device info", innerException)
        {}
    }
}