using System.Text;

namespace Meadow.Hcom;

internal class DeviceInfoSerialResponse : SerialResponse
{
    public Dictionary<string, string> Fields { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

    public string RawText => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal DeviceInfoSerialResponse(byte[] data, int length)
        : base(data, length)
    {
        var rawFields = RawText.Split('~', (char)StringSplitOptions.RemoveEmptyEntries);
        foreach (var f in rawFields)
        {
            var pair = f.Split('|', (char)StringSplitOptions.RemoveEmptyEntries);

            if ((pair.Length == 2) && !Fields.ContainsKey(pair[0]))
            {
                Fields.Add(pair[0], pair[1]);
            }
        }
    }
}
