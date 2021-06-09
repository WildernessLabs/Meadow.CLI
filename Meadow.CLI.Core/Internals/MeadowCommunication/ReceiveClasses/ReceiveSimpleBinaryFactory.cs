using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public class ReceiveSimpleBinaryFactory : ReceiveMessageFactory
    {
        public override IReceivedMessage Create(byte[] receivedMessage, int receivedMessageLength) => new ReceiveSimpleBinary(receivedMessage, receivedMessageLength);
    }
}
