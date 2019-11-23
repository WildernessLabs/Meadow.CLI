using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{
    // Factory class
    public class RecvSimpleBinaryFactory : RecvMessageFactory
    {
        public override IReceivedMessage Create(byte[] recvdMsg, int recvdMsgLength) => new RecvSimpleBinary(recvdMsg, recvdMsgLength);
    }

    // Concrete class
    internal class RecvSimpleBinary : RecvHeader
    {
        public RecvSimpleBinary(byte[] recvdMsg, int recvdMsgLength) : base(recvdMsg, recvdMsgLength)
        {
        }

        public override bool Execute(byte[] recvdMsg, int recvdMsgLen)
        {
            try
            {
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception:{ex.Message}");
                return false;
            }
        }
    }
}

