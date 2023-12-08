namespace Meadow.Hcom;

internal class UploadDataPacketResponse : SerialResponse
{
    internal UploadDataPacketResponse(byte[] data, int length)
        : base(data, length)
    {
    }

    public byte[] FileData => _data[RESPONSE_PAYLOAD_OFFSET..];
}
