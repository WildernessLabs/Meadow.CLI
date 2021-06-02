using System;
using Meadow.CLI.Core.Internals.MeadowComms;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDataProcessor
    {
        public EventHandler<MeadowMessageEventArgs>? OnReceiveData;
    }

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
}
