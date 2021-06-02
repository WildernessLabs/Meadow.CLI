using System;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public class ReceiveHeader : IReceivedMessage
    {
        // Header 12 bytes for Header plus message data
        public ushort SeqNumber { get; }
        public ushort VersionNumber { get; }
        public ushort RequestType { get; }
        public ushort ExtraData { get; }
        public uint UserData { get; }
        public int HeaderLength { get ; }

        public byte[]? MessageData { get; }
        public int MessageDataLength { get; }


        public ReceiveHeader(byte[] receivedMessage, int receivedMessageLength)
        {
            // Recover the sequence number which must proceed all messages.
            SeqNumber = Convert.ToUInt16(receivedMessage[HeaderLength] + (receivedMessage[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            VersionNumber = Convert.ToUInt16(receivedMessage[HeaderLength] + (receivedMessage[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            RequestType = Convert.ToUInt16(receivedMessage[HeaderLength] + (receivedMessage[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            ExtraData = Convert.ToUInt16(receivedMessage[HeaderLength] + (receivedMessage[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            UserData = Convert.ToUInt32(receivedMessage[HeaderLength] + (receivedMessage[HeaderLength + 1] << 8) +
                    (receivedMessage[HeaderLength + 2] << 16) + (receivedMessage[HeaderLength + 3] << 24));
            HeaderLength += sizeof(uint);

            MessageDataLength = receivedMessageLength - HeaderLength;
            if(MessageDataLength > 0)
            {
                MessageData = new byte[MessageDataLength];
                Array.Copy(receivedMessage, HeaderLength, MessageData, 0, MessageDataLength);
            }
            else
            {
                MessageData = null;
            }
        }

        public virtual bool Execute(byte[] receivedMessage, int receivedMessageLen)
        {
            return true;
        }
    }
}
