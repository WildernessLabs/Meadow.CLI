namespace Meadow.Hcom;

internal class DebuggingDataResponse : SerialResponse
{
    public byte[] Data { get; }

    internal DebuggingDataResponse(byte[] data, int length)
        : base(data, length)
    {
        var d = new byte[PayloadLength];
        Array.Copy(data, RESPONSE_PAYLOAD_OFFSET, d, 0, d.Length);
        Data = d;
    }
}
