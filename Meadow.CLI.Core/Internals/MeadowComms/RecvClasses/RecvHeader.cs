using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{
    public interface IReceivedMessage
    {
        // Each derived class needs these
        bool Execute(byte[] recvdMsg);
        string ToString();

        // These are in RecvHeader
        ushort SeqNumber { get; }
        ushort VersionNumber { get; }
        ushort ProtocolCtrl { get; }
        ushort RequestType { get; }
        uint UserData { get; }
        int HeaderLength { get; }
    }

    public class RecvHeader : IReceivedMessage
    {
        public ushort SeqNumber { get; private set; }
        public ushort VersionNumber { get; private set; }
        public ushort ProtocolCtrl { get; private set; }
        public ushort RequestType { get; private set; }
        public uint UserData { get; private set; }
        public int HeaderLength { get ; private set; }

        public RecvHeader(byte[] recvdMsg)
        {
            // Recover the sequence number which must proceed all messages.
            SeqNumber = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            VersionNumber = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            ProtocolCtrl = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            RequestType = Convert.ToUInt16(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8));
            HeaderLength += sizeof(ushort);

            UserData = Convert.ToUInt32(recvdMsg[HeaderLength] + (recvdMsg[HeaderLength + 1] << 8) +
                    (recvdMsg[HeaderLength + 2] << 16) + (recvdMsg[HeaderLength + 3] << 24));
            HeaderLength += sizeof(uint);
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public virtual bool Execute(byte[] recvdMsg)
        {
            return true;
        }
    }
}
