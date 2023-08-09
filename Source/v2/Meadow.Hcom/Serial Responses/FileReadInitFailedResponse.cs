namespace Meadow.Hcom;

internal class FileReadInitFailedResponse : SerialResponse
{
    internal FileReadInitFailedResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
