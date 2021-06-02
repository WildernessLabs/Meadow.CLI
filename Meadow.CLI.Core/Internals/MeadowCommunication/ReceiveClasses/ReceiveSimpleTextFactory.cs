using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public class ReceiveSimpleTextFactory : ReceiveMessageFactory
    {
        public override IReceivedMessage Create(byte[] receivedMessage, int receivedMessageLength) => new ReceiveSimpleText(receivedMessage, receivedMessageLength);
    }
}
