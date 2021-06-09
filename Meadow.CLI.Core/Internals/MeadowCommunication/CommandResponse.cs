namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    public class CommandResponse
    {
        public CommandResponse(bool isSuccess, string? message, MeadowMessageType messageType)
        {
            IsSuccess = isSuccess;
            Message = message;
            MessageType = messageType;
        }

        public bool IsSuccess { get; }
        public string? Message { get; }
        public MeadowMessageType MessageType { get;}
    }
}
