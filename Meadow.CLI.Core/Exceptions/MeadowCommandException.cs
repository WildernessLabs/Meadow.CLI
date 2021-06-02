using System;
using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CLI.Core.Exceptions
{
    public class MeadowCommandException : MeadowDeviceException
    {
        public MeadowCommandException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
