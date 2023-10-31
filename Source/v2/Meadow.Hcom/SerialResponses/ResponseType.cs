﻿namespace Meadow.Hcom;

public enum ResponseType : ushort
{
    HCOM_HOST_REQUEST_UNDEFINED_REQUEST = 0x00 | ProtocolType.HCOM_PROTOCOL_HEADER_UNDEFINED_TYPE,

    // Only header
    HCOM_HOST_REQUEST_UPLOAD_FILE_COMPLETED = 0x01 | ProtocolType.HCOM_PROTOCOL_HEADER_ONLY_TYPE,

    // Simple with some text message
    HCOM_HOST_REQUEST_TEXT_REJECTED = 0x01 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_ACCEPTED = 0x02 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_CONCLUDED = 0x03 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_ERROR = 0x04 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_INFORMATION = 0x05 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_LIST_HEADER = 0x06 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_LIST_MEMBER = 0x07 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_CRC_MEMBER = 0x08 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_MONO_STDOUT = 0x09 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_DEVICE_INFO = 0x0A | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_TRACE_MSG = 0x0B | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_RECONNECT = 0x0C | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_TEXT_MONO_STDERR = 0x0D | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,

    HCOM_HOST_REQUEST_INIT_DOWNLOAD_OKAY = 0x0E | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_INIT_DOWNLOAD_FAIL = 0x0F | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,

    HCOM_HOST_REQUEST_INIT_UPLOAD_OKAY = 0x10 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_INIT_UPLOAD_FAIL = 0x11 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_HOST_REQUEST_DNLD_FAIL_RESEND = 0x12 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,
    HCOM_MDOW_REQUEST_OTA_REGISTER_DEVICE = 0x13 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_TEXT_TYPE,

    // Simple with mono debug data
    HCOM_HOST_REQUEST_DEBUGGING_MONO_DATA = 0x01 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_BINARY_TYPE,
    HCOM_HOST_REQUEST_SEND_INITIAL_FILE_BYTES = 0x02 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_BINARY_TYPE,
    HCOM_HOST_REQUEST_UPLOADING_FILE_DATA = 0x03 | ProtocolType.HCOM_PROTOCOL_HEADER_SIMPLE_BINARY_TYPE,
}