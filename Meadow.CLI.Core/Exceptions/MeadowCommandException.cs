using System;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Internals.MeadowCommunication;

namespace Meadow.CLI.Core.Exceptions
{
    public class MeadowCommandException : MeadowDeviceException
    {
        public MeadowCommandException(string message, CommandResponse? commandResponse = null, Exception? innerException = null)
            : base(message, innerException)
        {
            MeadowMessage = commandResponse?.Message;
            MessageType = commandResponse?.MessageType;
        }

        public MeadowMessageType? MessageType { get; private set; }
        public string? MeadowMessage { get; private set; }
    }
}
