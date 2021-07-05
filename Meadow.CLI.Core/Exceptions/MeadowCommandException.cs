using System;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.MeadowCommunication;

namespace Meadow.CLI.Core.Exceptions
{
    public class MeadowCommandException : MeadowDeviceException
    {
        public MeadowCommandException(Command command, string message, CommandResponse? commandResponse = null, Exception? innerException = null)
            : base(message, innerException)
        {
            Command = command;
            MeadowMessage = commandResponse?.Message;
            MessageType = commandResponse?.MessageType;
        }

        public Command Command { get; }
        public MeadowMessageType? MessageType { get; }
        public string? MeadowMessage { get; }

        public override string ToString()
        {
            var b = base.ToString();
            return $"{Command}{Environment.NewLine}"
                 + $"{b}";
        }
    }
}
