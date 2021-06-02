using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public interface IReceivedMessage
    {
        // Each derived class needs these
        bool Execute(byte[] receivedMessage, int receivedMessageLen);
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
}
