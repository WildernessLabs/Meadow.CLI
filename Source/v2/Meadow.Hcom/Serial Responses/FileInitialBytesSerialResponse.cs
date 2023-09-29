using System.Text;

namespace Meadow.Hcom;

internal class FileInitialBytesSerialResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal FileInitialBytesSerialResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}

