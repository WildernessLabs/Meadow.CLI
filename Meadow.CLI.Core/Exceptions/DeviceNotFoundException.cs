using System;

namespace Meadow.CLI.Core.Exceptions
{
    public class DeviceNotFoundException : Exception
    {
        public DeviceNotFoundException(string message, Exception? innerException = null)
            : base(message, innerException)
        {

        }
    }
}
