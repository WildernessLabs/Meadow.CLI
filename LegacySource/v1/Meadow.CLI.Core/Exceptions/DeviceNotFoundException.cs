using System;
using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CLI.Core.Exceptions
{
    public class DeviceNotFoundException : MeadowDeviceException
    {
        public DeviceNotFoundException(string? message = null, Exception? innerException = null)
            : base(message ?? "Unable to find Meadow", innerException)
        {

        }
    }
}
