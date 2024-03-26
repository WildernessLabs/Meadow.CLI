using System;

namespace Meadow.CLI.Core.Exceptions
{
    public class MultipleDfuDevicesException : DeviceNotFoundException
    {
        public MultipleDfuDevicesException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
