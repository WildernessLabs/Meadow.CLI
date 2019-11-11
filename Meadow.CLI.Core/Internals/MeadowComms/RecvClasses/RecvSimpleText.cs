using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
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
                    throw new ArgumentException("Received RecvSimpleText with no text data");

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
            if (MessageDataLength > 0)
                return ASCIIEncoding.ASCII.GetString(MessageData);
            else
                return String.Empty;
        }
    }
}
