using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.Exceptions
{
    public class MeadowCloudException : Exception
    {
        public MeadowCloudException(string message, Exception? innerException = null)
            : base(message, innerException) { }
    }
}
