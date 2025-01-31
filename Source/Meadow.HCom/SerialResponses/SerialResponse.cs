namespace Meadow.Hcom;

internal class SerialResponse
{
    private const int HCOM_PROTOCOL_REQUEST_HEADER_SEQ_OFFSET = 0;
    private const int HCOM_PROTOCOL_REQUEST_HEADER_VERSION_OFFSET = 2;
    private const int HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET = 4;
    private const int HCOM_PROTOCOL_REQUEST_HEADER_EXTRA_DATA_OFFSET = 6;
    private const int HCOM_PROTOCOL_REQUEST_HEADER_USER_DATA_OFFSET = 8;
    protected const int RESPONSE_PAYLOAD_OFFSET = 12;

    protected byte[] _data;

    public ushort SequenceNumber => BitConverter.ToUInt16(_data, HCOM_PROTOCOL_REQUEST_HEADER_SEQ_OFFSET);
    public ushort ProtocolVersion => BitConverter.ToUInt16(_data, HCOM_PROTOCOL_REQUEST_HEADER_VERSION_OFFSET);
    public ResponseType RequestType => (ResponseType)BitConverter.ToUInt16(_data, HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET);
    public ushort ExtraData => BitConverter.ToUInt16(_data, HCOM_PROTOCOL_REQUEST_HEADER_EXTRA_DATA_OFFSET);
    public uint UserData => BitConverter.ToUInt32(_data, HCOM_PROTOCOL_REQUEST_HEADER_USER_DATA_OFFSET);
    protected int PayloadLength => _data.Length - RESPONSE_PAYLOAD_OFFSET;

    public static SerialResponse? Parse(byte[] data, int length)
    {
        if (length == 0)
        {
            return null;
        }

        var type = (ResponseType)BitConverter.ToUInt16(data, HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET);

        return type switch
        {
            ResponseType.HCOM_HOST_REQUEST_TEXT_MONO_STDERR => new TextStdErrResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_MONO_STDOUT => new TextStdOutResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_INFORMATION => new TextInformationResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_ACCEPTED => new TextRequestResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_REJECTED => new TextRequestRejectedResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_DEVICE_INFO => new DeviceInfoSerialResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_CONCLUDED => new TextConcludedResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_LIST_HEADER => new TextListHeaderResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_LIST_MEMBER => new TextListMemberResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_CRC_MEMBER => new TextCrcMemberResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_INIT_UPLOAD_FAIL => new FileReadInitFailedResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_INIT_UPLOAD_OKAY => new FileReadInitOkResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_UPLOADING_FILE_DATA => new UploadDataPacketResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_UPLOAD_FILE_COMPLETED => new UploadCompletedResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_ERROR => new RequestErrorTextResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_TEXT_RECONNECT => new ReconnectRequiredResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_INIT_DOWNLOAD_FAIL => new FileWriteInitFailedSerialResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_INIT_DOWNLOAD_OKAY => new FileWriteInitOkSerialResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_SEND_INITIAL_FILE_BYTES => new TextPayloadSerialResponse(data, length),
            ResponseType.HCOM_MDOW_REQUEST_OTA_REGISTER_DEVICE => new TextPayloadSerialResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_DNLD_FAIL_RESEND => new FileDownloadFailedResponse(data, length),
            ResponseType.HCOM_HOST_REQUEST_DEBUGGING_MONO_DATA => new DebuggingDataResponse(data, length),
            _ => new SerialResponse(data, length),
        };
    }

    protected SerialResponse(byte[] data, int length)
    {
        _data = new byte[length];
        Array.Copy(data, 0, _data, 0, length);
    }
}
