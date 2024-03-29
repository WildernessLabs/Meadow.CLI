﻿namespace Meadow.Hcom;

internal class FileWriteInitFailedSerialResponse : SerialResponse
{
    public string Text => Encoding.UTF8.GetString(_data, RESPONSE_PAYLOAD_OFFSET, PayloadLength);

    internal FileWriteInitFailedSerialResponse(byte[] data, int length)
        : base(data, length)
    {
        Debug.Write(Text);
    }
}
