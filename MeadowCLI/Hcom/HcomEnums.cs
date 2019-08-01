// This scheme is designed to reuse header definitions instead of a new definition
// for each message type. This reduces the amount of code needed, especially on the
// 'C' code where there no classes.
// It uses 2-bytes. The most-significant byte is the request header type. The remaining
// byte define the specific command
using System;

namespace MeadowCLI.Hcom
{

    public enum HcomProtocolHeaderTypes : UInt16
    {
        HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED = 0x0000,
        // Simple request types, include 4-byte user data
        HCOM_PROTOCOL_HEADER_TYPE_SIMPLE = 0x0100,
        // File releted request types, includes 4-byte user data (for the
        // destination partition id), 4-byte file size, 4-byte checksum and
        // variable length destition file name.
        HCOM_PROTOCOL_HEADER_TYPE_FILE = 0x0200,
    }

    // Messages to be sent to Meadow board
    public enum HcomMeadowRequestType : UInt16
    {
        HCOM_MDOW_REQUEST_UNDEFINED_REQUEST = 0x00 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED,

        HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS = 0x03 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_END_FILE_TRANSFER = 0x04 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU = 0x05 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH = 0x06 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_PARTITION_FLASH_FS = 0x07 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_MOUNT_FLASH_FS = 0x08 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS = 0x09 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_BULK_FLASH_ERASE = 0x0a | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_ENTER_DFU_MODE = 0x0b | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH = 0x0c | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_LIST_PARTITION_FILES = 0x0d | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC = 0x0e | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

        // Only used for testing
        HCOM_MDOW_REQUEST_DEVELOPER_1 = 0xf0 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_DEVELOPER_2 = 0xf1 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_DEVELOPER_3 = 0xf2 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_DEVELOPER_4 = 0xf3 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

        HCOM_MDOW_REQUEST_START_FILE_TRANSFER = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE,
        HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE,
    }

    public enum HcomRqstHeaderType
    {
        Undefined = 0x0000,
        Simple = 0x0000,
        FileType = 0xff0000,
    }
}