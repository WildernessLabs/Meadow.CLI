using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public class ReceiveSimpleTextUTF8Factory : ReceiveMessageFactory
    {
        public override IReceivedMessage Create(byte[] receivedMessage) => new ReceiveSimpleText(receivedMessage, Encoding.UTF8);
    }
}
