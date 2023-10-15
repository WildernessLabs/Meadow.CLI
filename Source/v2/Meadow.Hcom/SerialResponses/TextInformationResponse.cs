using System.Text;

namespace Meadow.Hcom;

/// <summary>
/// An unsolicited text response sent by HCOM (i.e. typically a Console.Write)
/// </summary>
internal class TextInformationResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal TextInformationResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
