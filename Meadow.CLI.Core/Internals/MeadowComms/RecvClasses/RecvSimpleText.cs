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
        public override IReceivedMessage Create(byte[] recvdMsg) => new RecvSimpleText(recvdMsg);
    }

    // Concrete class
    internal class RecvSimpleText : RecvHeader
    {
        string _msgString;

        public RecvSimpleText(byte[] recvdMsg) : base(recvdMsg)
        {
        }

        public override bool Execute(byte[] recvdMsg)
        {
            try
            {
                if (recvdMsg.Length == HeaderLength)
                    throw new ArgumentException("Received RecvSimpleText with no text data");

                _msgString = Encoding.UTF8.GetString(recvdMsg, HeaderLength, recvdMsg.Length - HeaderLength);
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
            return _msgString;
        }
    }
}
