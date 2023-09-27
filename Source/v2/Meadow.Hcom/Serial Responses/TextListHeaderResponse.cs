namespace Meadow.Hcom;

internal class TextListHeaderResponse : SerialResponse
{
    internal TextListHeaderResponse(byte[] data, int length)
        : base(data, length)
    {
    }
}
