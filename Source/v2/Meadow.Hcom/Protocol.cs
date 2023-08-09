namespace Meadow.Hcom
{
    internal class Protocol
    {
        // There is no length field. Since the packet boundaries are delimited and the
        // header is fixed length. Therefore, any additional data length is easily
        // determined.
        public const UInt16 HCOM_PROTOCOL_HCOM_VERSION_NUMBER = 0x0007;

        // COBS needs a specific delimiter. Zero seems to be traditional.
        public const UInt16 HCOM_PROTOCOL_COBS_ENCODING_DELIMITER_VALUE = 0x00;

        // What sequence number is used to identify a non-data message?
        public const UInt16 HCOM_PROTOCOL_NON_DATA_SEQUENCE_NUMBER = 0;

        // Note: while the MD5 hash is 128-bits (16-bytes), it is 32 character
        // hex string from ESP32
        public const UInt16 HCOM_PROTOCOL_COMMAND_MD5_HASH_LENGTH = 32;

        // Define the absolute maximum packet sizes for sent and receive.
        // Note: The length on the wire will be longer because it's encoded.
        public const int HCOM_PROTOCOL_PACKET_MAX_SIZE = 512;
        public const int HCOM_PROTOCOL_ENCODED_MAX_SIZE = HCOM_PROTOCOL_PACKET_MAX_SIZE + 8;

        // The maximum payload is max packet - header (12 bytes)
        public const int HCOM_PROTOCOL_DATA_MAX_SIZE = HCOM_PROTOCOL_PACKET_MAX_SIZE - 12;

        //static public int HcomProtoHdrMessageSize()
        //{
        //    return Marshal.SizeOf(typeof(HcomProtoHdrMessage));
        //}

        //static public int HcomProtoFSInfoMsgSize()
        //{
        //    return Marshal.SizeOf(typeof(HcomProtoFSInfoMsg));
        //}
    }
}