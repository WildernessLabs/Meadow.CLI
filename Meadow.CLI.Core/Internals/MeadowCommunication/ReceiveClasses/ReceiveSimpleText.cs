using System;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    internal class ReceiveSimpleText : ReceiveHeader
    {
        public ReceiveSimpleText(byte[] receivedMessage) : base(receivedMessage)
        {
        }

        public override bool Execute(byte[] receivedMessage)
        {
            try
            {
                //We actually have text based messages with 0 length
                if (receivedMessage.Length <= HeaderLength)
                {
                    throw new ArgumentException($"Received {nameof(ReceiveSimpleText)} with no text data");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception:{ex.Message}");
                return false;
            }
        }
        public override string ToString()
        {
            return (MessageDataLength > 0) ? ASCIIEncoding.ASCII.GetString(MessageData!) : string.Empty;
        }
    }
}
