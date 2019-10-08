using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{
    // Factory class
    public class RecvSimpleMsgFactory : RecvMessageFactory
    {
        public override IReceivedMessage Create(byte[] recvdMsg) => new RecvSimpleMsg(recvdMsg);
    }

    // Concrete class
    internal class RecvSimpleMsg : RecvHeader
    {
        public RecvSimpleMsg(byte[] recvdMsg) : base(recvdMsg)
        {
        }

        public override bool Execute(byte[] recvdMsg)
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

        public override string ToString()
        {
            return string.Empty;
        }

    }
}
