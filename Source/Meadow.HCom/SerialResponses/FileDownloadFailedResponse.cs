namespace Meadow.Hcom;

internal class FileDownloadFailedResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal FileDownloadFailedResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}

