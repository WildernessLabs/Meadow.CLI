namespace Meadow.Hcom;

/// <summary>
/// A text response to a solicited host request
/// </summary>
internal class TextRequestResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextRequestResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
