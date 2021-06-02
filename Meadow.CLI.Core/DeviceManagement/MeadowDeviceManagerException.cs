using System;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceManagerException : Exception
    {
        public MeadowDeviceManagerException(HcomMeadowRequestType hcomMeadowRequestType, Exception? innerException)  : base(null, innerException)
        {
            HcomMeadowRequestType = hcomMeadowRequestType;
        }

        public HcomMeadowRequestType HcomMeadowRequestType { get; set; }

        public override string ToString()
        {
            return $"A {GetType()} exception has occurred while making a {nameof(HcomMeadowRequestType)} of {HcomMeadowRequestType}.";
        }
    }
}
