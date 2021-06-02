using System;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowComms.RecvClasses
{
    // Factory class
    public class RecvSimpleTextFactory : RecvMessageFactory
    {
        public override IReceivedMessage Create(byte[] recvdMsg, int recvdMsgLength) => new RecvSimpleText(recvdMsg, recvdMsgLength);
    }

    // Concrete class
    internal class RecvSimpleText : RecvHeader
    {
        public RecvSimpleText(byte[] recvdMsg, int recvdMsgLength) : base(recvdMsg, recvdMsgLength)
        {
        }

        public override bool Execute(byte[] recvdMsg, int recvdMsgLen)
        {
            try
            {
                if (recvdMsg.Length == HeaderLength)
                {
                    throw new ArgumentException("Received RecvSimpleText with no text data");
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
            return (MessageDataLength > 0) ? ASCIIEncoding.ASCII.GetString(MessageData) : string.Empty;
        }
    }
}
