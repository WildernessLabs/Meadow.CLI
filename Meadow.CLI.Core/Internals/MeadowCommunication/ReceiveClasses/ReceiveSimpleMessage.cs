using System;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    internal class ReceiveSimpleMessage : ReceiveHeader
    {
        public ReceiveSimpleMessage(byte[] receivedMessage) : base(receivedMessage)
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
                Console.WriteLine ($"Unhandled Exception in ReceiveSimpleMessage.Execute():\n {ex.Message}\nStack Trace :\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
