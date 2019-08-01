using System;
using System.IO.Ports;

namespace MeadowCLI.Hcom
{
    public class TargetParsedArguments
    {
        public const UInt16 hcomRqstHeaderTypeMask = 0xff00;
        public const UInt32 unknownUserData = 0xffffffff;

        public TargetParsedArguments()
        {
            _userData = unknownUserData;
        }

        public bool IsUserDataSet => _userData == unknownUserData;

        // Return the request header type
        public HcomRqstHeaderType RequestHeaderType
        {
            get
            {
                if (((UInt16)_meadowRequestType & hcomRqstHeaderTypeMask) == (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED)
                    return HcomRqstHeaderType.Undefined;
                else if (((UInt16)_meadowRequestType & hcomRqstHeaderTypeMask) == (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE)
                    return HcomRqstHeaderType.Simple;
                else if (((UInt16)_meadowRequestType & hcomRqstHeaderTypeMask) == (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE)
                    return HcomRqstHeaderType.FileType;
                else
                {
                    throw new InvalidOperationException(string.Format("Unknown request header type: {0}",
                        _meadowRequestType));
                }
            }
        }

        public HcomMeadowRequestType _meadowRequestType { get; set; }
        public string _fileName { get; set; }
        public string _targetFileName { get; set; }
        public bool _isForced { get; set; }
        public SerialPort _serialPort { get; set; }
        public string _serialPortName { get; set; }
        public UInt32 _userData { get; set; }
        public uint _partition { get; set; }
    }

}
