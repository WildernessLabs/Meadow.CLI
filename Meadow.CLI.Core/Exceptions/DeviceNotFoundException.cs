using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core
{
    public class DeviceNotFoundException : Exception
    {
        public DeviceNotFoundException(string message, Exception innerException = null)
            : base(message, innerException)
        {

        }
    }
}
