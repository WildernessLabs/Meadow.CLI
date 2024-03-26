namespace Meadow.Hcom;

internal class FileReadInitOkResponse : SerialResponse
{
    internal FileReadInitOkResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}