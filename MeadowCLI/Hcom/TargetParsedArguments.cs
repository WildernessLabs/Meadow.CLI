using System;
using System.IO.Ports;

namespace MeadowCLI.Hcom
{
    public class TargetParsedArguments
    {
        public const UInt16 hcomRqstHeaderTypeMask = 0xff00;
        public const UInt32 unknownUserData = 0xffffffff;


        public HcomMeadowRequestType MeadowRequestType { get; set; }

        public string FileName { get; set; }

        public string TargetFileName { get; set; }

        public bool IsForced { get; private set; }

        public SerialPort SerialPort { get; set; }

        public string SerialPortName { get; set; }

        public UInt32 UserData { get; set; }

        public uint Partition { get; set; }


        public TargetParsedArguments()
        {
            UserData = unknownUserData;
        }

        public bool IsUserDataSet => UserData == unknownUserData;

        // Return the request header type
        public HcomRqstHeaderType RequestHeaderType
        {
            get
            {
                if (((UInt16)MeadowRequestType & hcomRqstHeaderTypeMask) ==
                    (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED)
                {
                    return HcomRqstHeaderType.Undefined;
                }

                if (((UInt16)MeadowRequestType & hcomRqstHeaderTypeMask) ==
                    (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE)
                {
                    return HcomRqstHeaderType.Simple;
                }

                if (((UInt16)MeadowRequestType & hcomRqstHeaderTypeMask) ==
                    (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE)
                {
                    return HcomRqstHeaderType.FileType;
                }

                throw new InvalidOperationException($"Unknown request header type: {MeadowRequestType}");
            }
        }
    }
}