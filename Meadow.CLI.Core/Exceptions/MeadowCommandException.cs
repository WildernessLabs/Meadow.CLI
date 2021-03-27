using System;
using System.Collections.Generic;
using System.Text;
using MeadowCLI.DeviceManagement;

namespace Meadow.CLI.Core
{
    public class MeadowCommandException : MeadowDeviceException
    {
        public MeadowCommandException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
