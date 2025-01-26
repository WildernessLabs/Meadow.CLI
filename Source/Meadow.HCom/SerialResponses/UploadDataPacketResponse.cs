namespace Meadow.Hcom;

internal class UploadDataPacketResponse : SerialResponse
{
    internal UploadDataPacketResponse(byte[] data, int length)
        : base(data, length)
    {
    }

    public byte[] FileData
    {
        get
        {
            int length = _data.Length - RESPONSE_PAYLOAD_OFFSET;
            byte[] result = new byte[length];
            Array.Copy(_data, RESPONSE_PAYLOAD_OFFSET, result, 0, length);
            return result;
        }
    }
}
