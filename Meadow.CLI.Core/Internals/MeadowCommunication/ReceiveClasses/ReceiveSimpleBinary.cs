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
                Console.WriteLine ($"Unhandled Exception in ReceiveSimpleBinary.Execute():\n {ex.Message}\nStack Trace :\n{ex.StackTrace}");
                return false;
            }
        }
    }
}

