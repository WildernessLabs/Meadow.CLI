using Meadow.CLI.Core.Internals.MeadowCommunication;
using System;
using System.Collections.Concurrent;

namespace Meadow.CLI.Core.DeviceManagement
{
    public abstract class MeadowDataProcessor : IDisposable
    {
        public bool SuppressOutput { get; set; } = false;
        public EventHandler<MeadowMessageEventArgs>? OnReceiveData;
        public BlockingCollection<byte[]> DebuggerMessages = new BlockingCollection<byte[]>();
        public abstract void Dispose();
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
