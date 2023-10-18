namespace Meadow.Hcom;

public class MeadowMessageEventArgs : EventArgs
{
    public string Message { get; private set; }
    public MeadowMessageType MessageType { get; private set; }

    public MeadowMessageEventArgs(MeadowMessageType messageType, string message = "")
    {
        Message = message;
        MessageType = messageType;
    }
}
