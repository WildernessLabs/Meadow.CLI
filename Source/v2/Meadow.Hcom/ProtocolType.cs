namespace Meadow.Hcom
{
    //--------------------------------------------------------------------------
    // HCOM Protocol message type definitions
    //--------------------------------------------------------------------------
    public enum ProtocolType : UInt16
    {
        // When the time comes the following Major types should reflect the
        // name of the above structure is used to send it. 
        HCOM_PROTOCOL_HEADER_UNDEFINED_TYPE = 0x0000,

        // The header of all mesasges include a 4-byte field called user data. The
        // User data field's meaning is determined by the message type

        // Header only request types,
        HCOM_PROTOCOL_HEADER_ONLY_TYPE = 0x0100,

        // File related types includes 4-byte user data (used for the destination
        // partition id), 4-byte file size, 4-byte checksum, 4-byte destination address
        // and variable length destination file name. Note: The  4-byte destination address
        // is currently only used for the STM32F7 to ESP32 downloads.
        HCOM_PROTOCOL_HEADER_FILE_START_TYPE = 0x0200,

        // Simple text is a header followed by text without a terminating NULL.
        HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE = 0x0300,

        // Simple binary is a header followed by binary data. The size of the data
        // can be up to HCOM_PROTOCOL_PACKET_MAX_SIZE minus header size
        HCOM_PROTOCOL_HEADER_SIMPLE_BINARY_TYPE = 0x0400,
    }
}