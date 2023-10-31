namespace Meadow.Hcom;

internal class UploadCompletedResponse : SerialResponse
{
    internal UploadCompletedResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
