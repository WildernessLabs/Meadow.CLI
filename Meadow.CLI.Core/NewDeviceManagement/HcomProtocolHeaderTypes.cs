using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.NewDeviceManagement
{
    public enum HcomProtocolHeaderTypes : UInt16
    {
        HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED = 0x0000,
        // Simple request types, include 4-byte user data
        HCOM_PROTOCOL_HEADER_TYPE_SIMPLE = 0x0100,
        // File releted request types, includes 4-byte user data (for the
        // destination partition id), 4-byte file size, 4-byte checksum and
        // variable length destination file name.
        HCOM_PROTOCOL_HEADER_TYPE_FILE_START = 0x0200,
        // Simple text. 
        HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT = 0x0300,
        // Header followed by binary data. The size of the data can be up to
        // HCOM_PROTOCOL_PACKET_MAX_SIZE minus header size
        HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_BINARY = 0x0400,
    }
}
