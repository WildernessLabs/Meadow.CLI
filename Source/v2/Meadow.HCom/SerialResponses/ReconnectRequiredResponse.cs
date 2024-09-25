namespace Meadow.Hcom;

internal class ReconnectRequiredResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal ReconnectRequiredResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
