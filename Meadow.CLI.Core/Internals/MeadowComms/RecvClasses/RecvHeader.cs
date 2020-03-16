using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using static MeadowCLI.DeviceManagement.MeadowFileManager;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{
    public interface IReceivedMessage
    {
        // Each derived class needs these
        bool Execute(byte[] recvdMsg, int receivedMsgLen);
        string ToString();

        // These are in RecvHeader
        ushort SeqNumber { get; }
        ushort VersionNumber { get; }
        ushort RequestType { get; }
        ushort ExtraData { get; }
        uint UserData { get; }
        int HeaderLength { get; }

        byte[] MessageData { get; }
    }

    public class RecvHeader : IReceivedMessage
    {
        // Header 12 bytes for Header plus message data
        public ushort SeqNumber { get; private set; }
        public ushort VersionNumber { get; private set; }
        public ushort RequestType { get; private set; }
        public ushort ExtraData { get; private set; }
        public uint UserData { get; private set; }
        public int HeaderLength { get ; private set; }

        public byte[] MessageData { get; private set; }
        public int MessageDataLength { get; private set; }


        public RecvHeader(byte[] recvdMsg, int recvdMsgLength)
        {
            // Recover the sequence number which must proceed all messages.
            SeqNumber = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            VersionNumber = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            RequestType = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            ExtraData = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            UserData = Convert.ToUInt32(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8) +
                    (recvdMsg[HeaderLength + 2] << 16) + (recvdMsg[HeaderLength + 3] << 24));
            HeaderLength += sizeof(UInt32);

            MessageDataLength = recvdMsgLength - HeaderLength;
            if(MessageDataLength > 0)
            {
                MessageData = new byte[MessageDataLength];
                Array.Copy(recvdMsg, HeaderLength, MessageData, 0, MessageDataLength);
            }
            else
            {
                MessageData = null;
            }
        }

        public virtual bool Execute(byte[] recvdMsg, int receivedMsgLen)
        {
            return true;
        }
    }
}
