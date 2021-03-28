using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    // TODO: Inherit from MeadowDeviceException
    public class DeviceInfoException : Exception
    {
        public DeviceInfoException(Exception? innerException = null) : base("An exception occurred while retrieving the device info", innerException)
        {}
    }
}
