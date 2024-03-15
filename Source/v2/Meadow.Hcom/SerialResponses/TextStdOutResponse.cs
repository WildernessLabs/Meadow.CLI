namespace Meadow.Hcom;

internal class TextStdOutResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextStdOutResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
