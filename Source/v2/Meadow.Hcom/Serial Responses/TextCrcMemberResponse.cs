using System.Text;

namespace Meadow.Hcom;

internal class TextCrcMemberResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextCrcMemberResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
