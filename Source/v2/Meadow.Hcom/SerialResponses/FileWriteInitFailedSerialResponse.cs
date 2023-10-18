namespace Meadow.Hcom;

internal class FileWriteInitFailedSerialResponse : SerialResponse
{
    internal FileWriteInitFailedSerialResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
