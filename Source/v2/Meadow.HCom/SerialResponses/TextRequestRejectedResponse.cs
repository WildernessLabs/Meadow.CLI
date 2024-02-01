using System.Diagnostics;
using System.Text;

namespace Meadow.Hcom;

internal class TextRequestRejectedResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextRequestRejectedResponse(byte[] data, int length)
        : base(data, length)
    {
        Debug.WriteLine(Text);
    }
}
