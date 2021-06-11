namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public abstract class ReceiveMessageFactory
    {
        public abstract IReceivedMessage Create(byte[] receivedMessage);
    }
}
