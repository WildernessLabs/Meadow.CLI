namespace Meadow.Hcom;

public interface IRequest
{
}

public abstract class Request : IRequest
{
    public abstract RequestType RequestType { get; }

    public ushort SequenceNumber { get; set; }
    public ushort ProtocolVersion { get; set; }
    public ushort ExtraData { get; set; } // TODO: what is this for?
    public uint UserData { get; set; }

    public Request()
    {
    }

    public byte[]? Payload { get; protected set; }

    public virtual byte[] Serialize()
    {
        var messageBytes = new byte[2 + 2 + 2 + 2 + 4 + (Payload?.Length ?? 0)];

        int offset = 0;

        // Two byte seq numb
        Array.Copy(
            BitConverter.GetBytes(SequenceNumber),
            0,
            messageBytes,
            offset,
            sizeof(ushort));

        offset += sizeof(ushort);

        // Protocol version
        Array.Copy(
            BitConverter.GetBytes(ProtocolVersion),
            0,
            messageBytes,
            offset,
            sizeof(ushort));

        offset += sizeof(ushort);

        // Command type (2 bytes)
        Array.Copy(
            BitConverter.GetBytes((ushort)RequestType),
            0,
            messageBytes,
            offset,
            sizeof(ushort));

        offset += sizeof(ushort);

        // Extra Data
        Array.Copy(
            BitConverter.GetBytes(ExtraData),
            0,
            messageBytes,
            offset,
            sizeof(ushort));

        offset += sizeof(ushort);

        // User Data
        Array.Copy(BitConverter.GetBytes(UserData), 0, messageBytes, offset, sizeof(uint));
        offset += sizeof(uint);

        if (Payload != null)
        {
            Array.Copy(
                Payload,
                0,
                messageBytes,
                offset,
                Payload.Length);
        }

        return messageBytes;
    }
}