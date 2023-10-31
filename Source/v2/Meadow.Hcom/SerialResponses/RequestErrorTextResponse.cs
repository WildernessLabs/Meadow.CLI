using System.Text;

namespace Meadow.Hcom;

internal class RequestErrorTextResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal RequestErrorTextResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
