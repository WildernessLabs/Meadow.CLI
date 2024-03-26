namespace Meadow.Hcom;

internal class TextAcceptedResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextAcceptedResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
