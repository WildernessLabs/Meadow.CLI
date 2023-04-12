using System;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    internal class ReceiveSimpleText : ReceiveHeader
    {
        private Encoding _encoding = Encoding.ASCII;

        public ReceiveSimpleText(byte[] receivedMessage) : base(receivedMessage)
        {
        }

        public ReceiveSimpleText(byte[] receivedMessage, Encoding encoding) : base(receivedMessage)
        {
            _encoding = encoding;
        }

        public override bool Execute(byte[] receivedMessage)
        {
            try
            {
                //We actually have text based messages with 0 length
                if (receivedMessage.Length < HeaderLength)
                {
                    throw new ArgumentException($"Received {nameof(ReceiveSimpleText)} with no text data. Message Length: {receivedMessage.Length}");
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
            return MessageDataLength > 0 ? _encoding.GetString(MessageData!) : string.Empty;
        }
    }
}
