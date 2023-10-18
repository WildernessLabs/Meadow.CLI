using System.Text;

namespace Meadow.Hcom;

internal class TextPayloadSerialResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextPayloadSerialResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}

