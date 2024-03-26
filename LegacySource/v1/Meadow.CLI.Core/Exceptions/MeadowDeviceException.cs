using System;

namespace Meadow.CLI.Core.Exceptions
{
    public class MeadowDeviceException : Exception
    {
        public MeadowDeviceException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
