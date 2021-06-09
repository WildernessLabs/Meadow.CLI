using System;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    internal class ReceiveSimpleMessage : ReceiveHeader
    {
        public ReceiveSimpleMessage(byte[] receivedMessage, int receivedMessageLength) : base(receivedMessage, receivedMessageLength)
        {
        }

        public override bool Execute(byte[] receivedMessage, int receivedMessageLen)
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
