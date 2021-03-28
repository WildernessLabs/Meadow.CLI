using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    public class MeadowDeviceManagerException : Exception
    {
        public MeadowDeviceManagerException(HcomMeadowRequestType hcomMeadowRequestType, Exception? innerException)  : base(null, innerException)
        {
            HcomMeadowRequestType = hcomMeadowRequestType;
        }

        public HcomMeadowRequestType HcomMeadowRequestType { get; set; }
    }
}
