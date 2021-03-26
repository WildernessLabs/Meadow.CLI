using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core
{
    public class MultipleDfuDevicesException : DeviceNotFoundException
    {
        public MultipleDfuDevicesException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
