using System;
using System.Security.Cryptography;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    internal class ReceiveSimpleBinary : ReceiveHeader
    {
        public ReceiveSimpleBinary(byte[] receivedMessage) : base(receivedMessage)
        {
        }

        public override bool Execute(byte[] receivedMessage)
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

